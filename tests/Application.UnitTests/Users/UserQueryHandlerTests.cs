using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users.GetAll;
using Application.Users.GetById;
using Application.UnitTests.Abstractions;
using Domain.Users;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class UserQueryHandlerTests : BaseHandlerTest
{
    private static User NewUser(string firstName, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        EntraObjectId = Guid.NewGuid(),
        EntraTenantId = Guid.NewGuid(),
        Email = $"{firstName}@example.com",
        FirstName = firstName,
        LastName = "Tester",
        IsActive = isActive
    };

    private static IUserContext UserContextFor(Guid userId, params string[] roles)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        userContext.Roles.Returns(roles);

        return userContext;
    }

    [Fact]
    public async Task GetUsers_Should_ExcludeDeactivatedPeople()
    {
        // Arrange — deprovisioned rows are switched off, never deleted, so the list has to filter
        // them out or somebody who left the organization keeps showing up as a colleague.
        await using TestDbContext context = CreateDbContext();
        context.Users.AddRange(NewUser("Active"), NewUser("Gone", isActive: false));
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        // Act
        Result<PagedResponse<UserResponse>> result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].FirstName.ShouldBe("Active");
        result.Value.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetUsers_Should_ClampAnUnreasonablePageSize()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(NewUser("Solo"));
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        // Act — an unbounded page size is a denial-of-service invitation on a real directory.
        Result<PagedResponse<UserResponse>> result =
            await handler.Handle(new GetUsersQuery(PageNumber: 1, PageSize: 100_000), CancellationToken.None);

        // Assert
        result.Value.PageSize.ShouldBe(20);
    }

    [Fact]
    public async Task GetUserById_Should_ReturnOwnProfile_WithoutAnyRole()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User me = NewUser("Self");
        context.Users.Add(me);
        await context.SaveChangesAsync();

        var handler = new GetUserByIdQueryHandler(context, UserContextFor(me.Id));

        // Act
        Result<UserDetailResponse> result = await handler.Handle(new GetUserByIdQuery(me.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe(me.Email);
    }

    [Fact]
    public async Task GetUserById_Should_ReturnUnauthorized_WhenReadingSomebodyElseWithoutAdmin()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User other = NewUser("Other");
        context.Users.Add(other);
        await context.SaveChangesAsync();

        var handler = new GetUserByIdQueryHandler(context, UserContextFor(Guid.NewGuid(), RoleNames.Member));

        // Act
        Result<UserDetailResponse> result = await handler.Handle(new GetUserByIdQuery(other.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task GetUserById_Should_ReadTheRoleFromTheToken_NotTheDatabase()
    {
        // Arrange — the local row carries no roles at all; the claim is the only source. If this
        // ever needs a database column to pass, the local role store is back.
        await using TestDbContext context = CreateDbContext();
        User other = NewUser("Other");
        context.Users.Add(other);
        await context.SaveChangesAsync();

        var handler = new GetUserByIdQueryHandler(context, UserContextFor(Guid.NewGuid(), RoleNames.Admin));

        // Act
        Result<UserDetailResponse> result = await handler.Handle(new GetUserByIdQuery(other.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FirstName.ShouldBe("Other");
    }

    [Fact]
    public async Task GetUserById_Should_ReturnNotFound_ForAnUnknownId()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var missingId = Guid.NewGuid();

        var handler = new GetUserByIdQueryHandler(context, UserContextFor(Guid.NewGuid(), RoleNames.Admin));

        // Act
        Result<UserDetailResponse> result = await handler.Handle(new GetUserByIdQuery(missingId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(missingId));
    }
}
