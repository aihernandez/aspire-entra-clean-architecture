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

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(int.MinValue, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, 20)]
    public async Task GetUsers_Should_RejectInvalidPagination(int pageNumber, int pageSize)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(NewUser("Solo"));
        await context.SaveChangesAsync();

        var handler = new GetUsersQueryHandler(context);

        Result<PagedResponse<UserResponse>> result =
            await handler.Handle(new GetUsersQuery(pageNumber, pageSize), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidPagination);
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 100, 1)]
    [InlineData(2, 1, 0)]
    [InlineData(int.MaxValue, 1, 0)]
    public async Task GetUsers_Should_AcceptValidPageBoundaries(int pageNumber, int pageSize, int expectedCount)
    {
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(NewUser("Solo"));
        await context.SaveChangesAsync();
        var handler = new GetUsersQueryHandler(context);

        Result<PagedResponse<UserResponse>> result =
            await handler.Handle(new GetUsersQuery(pageNumber, pageSize), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(expectedCount);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.PageNumber.ShouldBe(pageNumber);
        result.Value.PageSize.ShouldBe(pageSize);
    }

    [Fact]
    public async Task GetUsers_Should_BreakNameTiesById_AcrossPages()
    {
        await using TestDbContext context = CreateDbContext();
        User first = NewUser("Same");
        first.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        User second = NewUser("Same");
        second.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        context.Users.AddRange(second, first);
        await context.SaveChangesAsync();
        var handler = new GetUsersQueryHandler(context);

        Result<PagedResponse<UserResponse>> pageOne = await handler.Handle(new GetUsersQuery(1, 1), CancellationToken.None);
        Result<PagedResponse<UserResponse>> pageTwo = await handler.Handle(new GetUsersQuery(2, 1), CancellationToken.None);

        pageOne.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(first.Id);
        pageTwo.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(second.Id);
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
