using System.Security.Claims;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Infrastructure.Authentication;

/// <summary>
/// Just-in-time provisioning. With Entra ID there is no registration step — the directory created
/// the person long before they opened this application — so their first authenticated request
/// <i>is</i> their registration, and this is where the local record comes into existence.
/// <para>
/// The lookup key is the pair (<c>oid</c>, <c>tid</c>), never <c>oid</c> alone and never the email
/// address: Microsoft's guidance is explicit that <c>email</c>/<c>preferred_username</c>/<c>upn</c>
/// are display values which tenant administrators can change and reassign, making them unsafe to
/// key data on.
/// </para>
/// </summary>
internal sealed class UserProvisioningMiddleware(RequestDelegate next, ILogger<UserProvisioningMiddleware> logger)
{
    /// <summary>
    /// How stale the "last seen" stamp is allowed to get before it is worth a write. Without this,
    /// every single request would dirty the user row.
    /// </summary>
    private static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity is not { IsAuthenticated: true } || context.User.IsAppOnlyToken())
        {
            // Anonymous, or a service principal. Neither gets a user profile; endpoints that need
            // one require a permission and will refuse the request on their own.
            await next(context);
            return;
        }

        if (context.User.GetAppRoles().Count == 0)
        {
            // Authenticated against the tenant, but never assigned to this application. They cannot
            // reach any endpoint (every one requires a permission), so creating a local record for
            // them would only fill the table with people who have no access — and quietly make
            // "users of this app" mean something else.
            await next(context);
            return;
        }

        Guid? objectId = context.User.GetEntraObjectId();
        Guid? tenantId = context.User.GetEntraTenantId();

        if (objectId is null || tenantId is null)
        {
            logger.LogWarning("An authenticated request carried no oid/tid pair; no user was provisioned.");
            await next(context);
            return;
        }

        IServiceProvider services = context.RequestServices;
        ApplicationDbContext dbContext = services.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider dateTimeProvider = services.GetRequiredService<IDateTimeProvider>();

        User? user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.EntraObjectId == objectId.Value && u.EntraTenantId == tenantId.Value,
            context.RequestAborted);

        if (user is null)
        {
            user = await ProvisionAsync(dbContext, objectId.Value, tenantId.Value, dateTimeProvider, context);
        }
        else if (!user.IsActive)
        {
            // Deprovisioned locally (by a future SCIM integration, or by hand). Entra may still
            // issue them a token, so the refusal has to happen here.
            logger.LogWarning("Rejected a request from deactivated user {UserId}.", user.Id);

            await Results
                .Problem(statusCode: StatusCodes.Status403Forbidden, detail: UserErrors.Deactivated.Description)
                .ExecuteAsync(context);

            return;
        }
        else
        {
            await RefreshAsync(dbContext, user, dateTimeProvider, context);
        }

        context.Items[UserContext.LocalUserIdItemKey] = user.Id;

        await next(context);
    }

    private async Task<User> ProvisionAsync(
        ApplicationDbContext dbContext,
        Guid objectId,
        Guid tenantId,
        IDateTimeProvider dateTimeProvider,
        HttpContext context)
    {
        (string email, string firstName, string lastName) = ReadProfile(context);

        DateTime now = dateTimeProvider.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            EntraObjectId = objectId,
            EntraTenantId = tenantId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };

        user.Raise(new UserProvisionedDomainEvent(user.Id));

        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync(context.RequestAborted);

        // The welcome email is sent by a handler for UserProvisionedDomainEvent, dispatched by
        // SaveChangesAsync above — middleware has no business talking to an SMTP server.
        logger.LogInformation("Provisioned local user {UserId} for Entra object {ObjectId}.", user.Id, objectId);

        return user;
    }

    private static async Task RefreshAsync(
        ApplicationDbContext dbContext,
        User user,
        IDateTimeProvider dateTimeProvider,
        HttpContext context)
    {
        (string email, string firstName, string lastName) = ReadProfile(context);

        DateTime now = dateTimeProvider.UtcNow;

        // The local copy of name and email is a cache of directory values, so the directory wins on
        // every request that disagrees with it. Nothing here is used for authorization.
        bool profileChanged =
            email.Length > 0 && !string.Equals(user.Email, email, StringComparison.Ordinal) ||
            firstName.Length > 0 && !string.Equals(user.FirstName, firstName, StringComparison.Ordinal) ||
            lastName.Length > 0 && !string.Equals(user.LastName, lastName, StringComparison.Ordinal);

        if (profileChanged)
        {
            user.Email = email.Length > 0 ? email : user.Email;
            user.FirstName = firstName.Length > 0 ? firstName : user.FirstName;
            user.LastName = lastName.Length > 0 ? lastName : user.LastName;
        }

        if (!profileChanged && now - user.LastSeenAtUtc < LastSeenWriteInterval)
        {
            return;
        }

        user.LastSeenAtUtc = now;

        await dbContext.SaveChangesAsync(context.RequestAborted);
    }

    /// <summary>
    /// Display-only values. Read from whichever claims the tenant happens to emit — these are
    /// exactly the claims Microsoft warns must never drive an authorization or storage decision,
    /// so they are used for nothing but rendering a name on screen.
    /// </summary>
    private static (string Email, string FirstName, string LastName) ReadProfile(HttpContext context)
    {
        string email =
            context.User.FindFirstValue("preferred_username") ??
            context.User.FindFirstValue(ClaimTypes.Email) ??
            context.User.FindFirstValue("upn") ??
            string.Empty;

        string firstName =
            context.User.FindFirstValue("given_name") ??
            context.User.FindFirstValue(ClaimTypes.GivenName) ??
            string.Empty;

        string lastName =
            context.User.FindFirstValue("family_name") ??
            context.User.FindFirstValue(ClaimTypes.Surname) ??
            string.Empty;

        if (firstName.Length == 0 && lastName.Length == 0)
        {
            string displayName = context.User.FindFirstValue("name") ?? string.Empty;
            string[] parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            firstName = parts.Length > 0 ? parts[0] : string.Empty;
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        return (email, firstName, lastName);
    }
}
