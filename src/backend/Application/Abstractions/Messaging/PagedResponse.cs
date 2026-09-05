namespace Application.Abstractions.Messaging;

/// <summary>
/// A generic paged result wrapper, reusable by any query that lists a large collection (Users
/// today; any future list endpoint can return this same shape instead of a bare array).
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
