using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.UpdateStatus;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class UpdateUserStatusCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_DeactivateAndReactivateUser_WithoutDeletingHistoricalRow()
    {
        await using TestDbContext context = CreateDbContext();
        var target = new User { Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), EntraTenantId = TenantId };
        context.Users.Add(target);
        await context.SaveChangesAsync();

        IUserContext admin = Substitute.For<IUserContext>();
        admin.UserId.Returns(Guid.NewGuid());
        admin.TenantId.Returns(TenantId);
        admin.Roles.Returns([RoleNames.Admin]);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        var deactivatedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        clock.UtcNow.Returns(deactivatedAt);
        var handler = new UpdateUserStatusCommandHandler(context, admin, clock);

        Result deleted = await handler.Handle(new UpdateUserStatusCommand(target.Id, false), CancellationToken.None);
        deleted.IsSuccess.ShouldBeTrue();
        User row = await context.Users.SingleAsync(user => user.Id == target.Id);
        row.IsActive.ShouldBeFalse();
        row.DeactivatedAtUtc.ShouldBe(deactivatedAt);

        Result reactivated = await handler.Handle(new UpdateUserStatusCommand(target.Id, true), CancellationToken.None);
        reactivated.IsSuccess.ShouldBeTrue();
        row.IsActive.ShouldBeTrue();
        row.DeactivatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_RejectNonAdministratorAndSelfDeactivation()
    {
        await using TestDbContext context = CreateDbContext();
        var target = new User { Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), EntraTenantId = TenantId };
        context.Users.Add(target);
        await context.SaveChangesAsync();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();

        IUserContext member = Substitute.For<IUserContext>();
        member.UserId.Returns(Guid.NewGuid());
        member.TenantId.Returns(TenantId);
        member.Roles.Returns([RoleNames.Member]);
        var memberHandler = new UpdateUserStatusCommandHandler(context, member, clock);
        Result memberResult = await memberHandler.Handle(new UpdateUserStatusCommand(target.Id, false), CancellationToken.None);
        memberResult.Error.ShouldBe(UserErrors.Unauthorized());

        IUserContext admin = Substitute.For<IUserContext>();
        admin.UserId.Returns(target.Id);
        admin.TenantId.Returns(TenantId);
        admin.Roles.Returns([RoleNames.Admin]);
        var adminHandler = new UpdateUserStatusCommandHandler(context, admin, clock);
        Result selfResult = await adminHandler.Handle(new UpdateUserStatusCommand(target.Id, false), CancellationToken.None);
        selfResult.Error.ShouldBe(UserErrors.CannotDeactivateSelf);
        target.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_ForMissingUser()
    {
        await using TestDbContext context = CreateDbContext();
        IUserContext admin = Substitute.For<IUserContext>();
        admin.UserId.Returns(Guid.NewGuid());
        admin.TenantId.Returns(TenantId);
        admin.Roles.Returns([RoleNames.Admin]);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        var handler = new UpdateUserStatusCommandHandler(context, admin, clock);
        var missingId = Guid.NewGuid();

        Result result = await handler.Handle(new UpdateUserStatusCommand(missingId, false), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NotFound(missingId));
    }

    [Fact]
    public async Task Handle_Should_NotChangeAnotherTenantsUser()
    {
        await using TestDbContext context = CreateDbContext();
        var target = new User
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), EntraTenantId = Guid.NewGuid()
        };
        context.Users.Add(target);
        await context.SaveChangesAsync();
        IUserContext admin = Substitute.For<IUserContext>();
        admin.UserId.Returns(Guid.NewGuid());
        admin.TenantId.Returns(TenantId);
        admin.Roles.Returns([RoleNames.Admin]);
        var handler = new UpdateUserStatusCommandHandler(context, admin, Substitute.For<IDateTimeProvider>());

        Result result = await handler.Handle(new UpdateUserStatusCommand(target.Id, false), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NotFound(target.Id));
        target.IsActive.ShouldBeTrue();
    }
}
