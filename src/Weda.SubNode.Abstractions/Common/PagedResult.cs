namespace Weda.SubNode.Abstractions.Common;

/// <summary>
/// Represents a paged result containing items and pagination metadata.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="TotalCount">The total count of items across all pages.</param>
/// <param name="PageIndex">The current page index (0-based).</param>
/// <param name="PageSize">The number of items per page.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize
)
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageIndex > 0;

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageIndex < TotalPages - 1;

    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    public static PagedResult<T> Empty(int pageIndex = 0, int pageSize = 20) => new(
        Items: [],
        TotalCount: 0,
        PageIndex: pageIndex,
        PageSize: pageSize
    );
}