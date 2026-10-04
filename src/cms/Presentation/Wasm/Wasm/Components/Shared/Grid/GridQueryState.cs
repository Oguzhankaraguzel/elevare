namespace Wasm.Components.Shared.Grid;

/// <summary>
/// Emitted by the grid in <see cref="ElevareGrid{TItem}.ServerMode"/> when the user
/// changes page, page-size, sort or filters. The parent must reload
/// <c>Data</c> + <c>TotalCount</c> accordingly.
/// </summary>
public sealed record GridQueryState
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortProperty { get; init; }
    public SortDirection SortDirection { get; init; }
    public string? GlobalSearch { get; init; }
    public IReadOnlyDictionary<string, string> ColumnFilters { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Columns the user is grouping by, outermost first — empty when not grouping.
    /// <para>
    /// Grouping is done in the browser over the rows the grid currently holds, which
    /// in server mode is one page. A parent that supports grouping therefore has to
    /// notice this is non-empty and hand over the whole set instead of a page;
    /// otherwise the user gets groups built from 24 rows out of hundreds, which
    /// looks like an answer but is not one. The grid hides pagination while
    /// grouping for the same reason.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> GroupByColumns { get; init; } = [];
}
