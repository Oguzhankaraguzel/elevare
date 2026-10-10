using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Wasm.Components.Shared.Grid;

public partial class ElevareGrid<TItem> : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    #region Parameters

    [Parameter] public IEnumerable<TItem> Data { get; set; } = [];
    [Parameter] public List<GridColumnDef<TItem>>? Columns { get; set; }
    [Parameter] public Action<List<GridColumnDef<TItem>>>? ConfigureColumns { get; set; }
    [Parameter] public Dictionary<string, RenderFragment<TItem>>? CellTemplates { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public bool ShowToolbar { get; set; } = true;
    [Parameter] public bool ShowPagination { get; set; } = true;
    [Parameter] public bool ShowGlobalSearch { get; set; } = true;
    [Parameter] public bool ShowGroupPanel { get; set; } = true;
    [Parameter] public bool ShowExport { get; set; } = true;
    [Parameter] public bool ShowImport { get; set; } = true;
    [Parameter] public bool ShowColumnToggle { get; set; } = true;
    [Parameter] public bool ShowDensityToggle { get; set; } = true;
    [Parameter] public bool ShowRowNumbers { get; set; }
    [Parameter] public SelectionMode SelectionMode { get; set; }
    [Parameter] public GridDensity Density { get; set; } = GridDensity.Normal;
    [Parameter] public int PageSize { get; set; } = 20;
    [Parameter] public int[] PageSizeOptions { get; set; } = [10, 20, 50, 100];
    [Parameter] public bool Striped { get; set; }
    [Parameter] public bool Hover { get; set; } = true;
    [Parameter] public bool Bordered { get; set; }
    [Parameter] public string? Height { get; set; }
    [Parameter] public string EmptyText { get; set; } = "No data found";
    [Parameter] public string LoadingText { get; set; } = "Loading…";
    [Parameter] public bool Loading { get; set; }
    [Parameter] public RenderFragment? ToolbarStart { get; set; }
    [Parameter] public RenderFragment? ToolbarEnd { get; set; }
    [Parameter] public RenderFragment<TItem>? RowDetail { get; set; }
    [Parameter] public Func<TItem, string>? RowCssClass { get; set; }

    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
    [Parameter] public EventCallback<TItem> OnRowDoubleClick { get; set; }
    [Parameter] public EventCallback<HashSet<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<List<TItem>> OnDataImported { get; set; }

    /// <summary>When true, rows can be edited inline.</summary>
    [Parameter] public bool AllowInlineEdit { get; set; }
    /// <summary>Fired when the user clicks Save Changes. Contains all modified rows with their edited values.</summary>
    [Parameter] public EventCallback<List<EditedRow<TItem>>> OnSaveChanges { get; set; }
    /// <summary>Fired when the user clicks Discard Changes.</summary>
    [Parameter] public EventCallback OnDiscardChanges { get; set; }

    /// <summary>Custom action buttons column rendered as the last column of each row.</summary>
    [Parameter] public RenderFragment<TItem>? ActionTemplate { get; set; }

    /// <summary>
    /// When true the grid delegates filtering/sorting/paging to the server.
    /// The grid fires <see cref="OnStateChanged"/> on every interaction; the parent
    /// must reload <see cref="Data"/> + <see cref="TotalCount"/> accordingly.
    /// </summary>
    [Parameter] public bool ServerMode { get; set; }

    /// <summary>Server-side total record count (only used when <see cref="ServerMode"/> is true).</summary>
    [Parameter] public int TotalCount { get; set; }

    /// <summary>Raised in <see cref="ServerMode"/> when the user changes page, sort or filters.</summary>
    [Parameter] public EventCallback<GridQueryState> OnStateChanged { get; set; }

    #endregion

    #region State

    private IEnumerable<TItem>? _lastDataRef;
    private int _lastPageSize;
    private List<GridColumnDef<TItem>> _columns = [];
    private List<TItem> _allData = [];
    private List<TItem> _filteredData = [];
    private List<TItem> _sortedData = [];
    private List<TItem> _pagedData = [];
    private List<DisplayRow<TItem>> _flatRows = [];

    private string _globalSearch = "";
    private readonly List<SortDescriptor> _sorts = [];
    private readonly Dictionary<string, FilterDescriptor> _filters = [];
    private readonly List<string> _groupByColumns = [];

    private int _currentPage = 1;
    private int _currentPageSize;
    private int _totalFilteredCount;
    private int TotalPages => _currentPageSize > 0
        ? (int)Math.Ceiling((double)_totalFilteredCount / _currentPageSize)
        : 1;

    private readonly HashSet<TItem> _selectedItems = [];
    private readonly HashSet<int> _expandedDetailRows = [];
    private readonly HashSet<string> _collapsedGroups = [];

    private GridDensity _density;
    private bool _showFilterRow;
    private bool _showColumnMenu;
    private CancellationTokenSource? _searchDebounce;

    private readonly HashSet<TItem> _editingRows = [];
#pragma warning disable CS8714
    private readonly Dictionary<TItem, Dictionary<string, object?>> _pendingEdits = new();
#pragma warning restore CS8714

    #endregion

    #region Lifecycle

    protected override void OnInitialized()
    {
        _currentPageSize = PageSize;
        _lastPageSize = PageSize;
        _density = Density;
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(Data, _lastDataRef))
        {
            _lastDataRef = Data;
            _allData = Data?.ToList() ?? [];
            BuildColumns();
            ProcessData();
        }

        if (_lastPageSize != PageSize)
        {
            _lastPageSize = PageSize;
            _currentPageSize = PageSize;
            _currentPage = 1;
            ProcessData();
        }
    }

    private string GetStickyStyle(GridColumnDef<TItem> col)
    {
        if (!col.Sticky) return string.Empty;
        // simple sticky on left; offset is calculated by order of sticky columns
        int idx = _columns.Where(c => c.Visible && c.Sticky).ToList().FindIndex(c => c.PropertyName == col.PropertyName);
        int offset = 0;
        for (int i = 0; i < idx; i++) offset += 56; // approximate cell width for sticky offset
        return $"position:sticky;left:{offset}px;z-index:3;background:var(--cms-card);";
    }

    public void Dispose()
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
    }

    #endregion

    #region Column Management

    private void BuildColumns()
    {
        _columns = Columns is not null && Columns.Count > 0
            ? [.. Columns]
            : AutoGenerateColumns();

        if (CellTemplates is not null)
        {
            foreach (KeyValuePair<string, RenderFragment<TItem>> pair in CellTemplates)
            {
                GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == pair.Key);
                if (col is not null)
                    col.CellTemplate = pair.Value;
            }
        }

        ConfigureColumns?.Invoke(_columns);
    }

    private static List<GridColumnDef<TItem>> AutoGenerateColumns()
    {
        return typeof(TItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !IsComplexType(p.PropertyType))
            .Select(p => new GridColumnDef<TItem>
            {
                PropertyName = p.Name,
                Title = SplitCamelCase(p.Name),
                PropertyType = p.PropertyType,
                ValueAccessor = CreateAccessor(p),
                Align = DefaultAlign(p.PropertyType),
                Format = DefaultFormat(p.PropertyType),
            })
            .ToList();
    }

    private static Func<TItem, object?> CreateAccessor(PropertyInfo prop)
    {
        return item => prop.GetValue(item);
    }

    private static bool IsComplexType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(Guid) || type == typeof(TimeSpan) || type.IsEnum)
            return false;
        if (typeof(IEnumerable<string>).IsAssignableFrom(type))
            return false;
        return true;
    }

    private static TextAlign DefaultAlign(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsNumericType(type)) return TextAlign.Right;
        if (type == typeof(bool)) return TextAlign.Center;
        return TextAlign.Left;
    }

    private static string? DefaultFormat(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "dd.MM.yyyy";
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "N2";
        return null;
    }

    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        StringBuilder sb = new(input.Length + 4);
        sb.Append(input[0]);
        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && i > 0 && !char.IsUpper(input[i - 1]))
                sb.Append(' ');
            sb.Append(input[i]);
        }
        return sb.ToString();
    }

    private int VisibleColumnCount
    {
        get
        {
            int c = _columns.Count(x => x.Visible);
            if (SelectionMode != SelectionMode.None) c++;
            if (ShowRowNumbers) c++;
            if (RowDetail is not null) c++;
            if (ActionTemplate is not null || AllowInlineEdit) c++;
            return c;
        }
    }

    #endregion

    #region Data Processing

    private void ProcessData()
    {
        if (ServerMode)
        {
            // In server mode the grid only displays what Data contains.
            // Filtering/sorting/paging are handled by the parent via OnStateChanged.
            _filteredData = _allData;
            _sortedData   = _allData;
            _pagedData    = _allData;
            _totalFilteredCount = TotalCount > 0 ? TotalCount : _allData.Count;

            // Grouping still runs, but only over the page the server already sent —
            // there is no full dataset to group here, and fetching one just for this
            // would defeat the point of paging server-side. Groups therefore reflect
            // "what's on this page", the same honest scope paging itself already has.
            _flatRows = _groupByColumns.Count > 0
                ? FlattenGroups(BuildGroups(_allData, _groupByColumns, 0))
                : [];
            return;
        }

        _filteredData = ApplyFilters(_allData);
        _totalFilteredCount = _filteredData.Count;
        _sortedData = ApplySorting(_filteredData);

        if (_groupByColumns.Count > 0)
        {
            List<GroupBucket<TItem>> groups = BuildGroups(_sortedData, _groupByColumns, 0);
            _flatRows = FlattenGroups(groups);
            _pagedData = [];
        }
        else
        {
            _flatRows = [];
            ApplyPaging();
        }

        if (_currentPage > TotalPages && TotalPages > 0)
        {
            _currentPage = TotalPages;
            if (_groupByColumns.Count == 0) ApplyPaging();
        }
    }

    #endregion

    #region Filtering

    private List<TItem> ApplyFilters(List<TItem> data)
    {
        IEnumerable<TItem> result = data;

        if (!string.IsNullOrWhiteSpace(_globalSearch))
        {
            string search = _globalSearch;
            result = result.Where(item =>
                _columns.Where(c => c.Visible && c.Filterable).Any(col =>
                {
                    object? val = col.ValueAccessor(item);
                    if (val is IEnumerable<string> list)
                        return list.Any(s => s.Contains(search, StringComparison.OrdinalIgnoreCase));
                    return val?.ToString()?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
                }));
        }

        foreach (KeyValuePair<string, FilterDescriptor> entry in _filters)
        {
            FilterDescriptor filter = entry.Value;
            if (string.IsNullOrWhiteSpace(filter.Value) &&
                filter.Operator is not FilterOperator.IsNull and not FilterOperator.IsNotNull)
                continue;

            GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == entry.Key);
            if (col is null) continue;

            result = result.Where(item => MatchesFilter(col, item, filter));
        }

        return result.ToList();
    }

    private static bool MatchesFilter(GridColumnDef<TItem> col, TItem item, FilterDescriptor filter)
    {
        object? raw = col.ValueAccessor(item);
        if (filter.Operator == FilterOperator.IsNull) return raw is null;
        if (filter.Operator == FilterOperator.IsNotNull) return raw is not null;
        if (raw is null) return false;

        if (raw is IEnumerable<string> list)
        {
            return list.Any(s => s.Contains(filter.Value, StringComparison.OrdinalIgnoreCase));
        }

        string strVal = raw.ToString() ?? "";
        Type type = Nullable.GetUnderlyingType(col.PropertyType) ?? col.PropertyType;

        if (IsNumericType(type))
        {
            if (!double.TryParse(strVal, CultureInfo.InvariantCulture, out double nv) ||
                !double.TryParse(filter.Value, CultureInfo.InvariantCulture, out double nf))
                return false;

            return filter.Operator switch
            {
                FilterOperator.Equals => Math.Abs(nv - nf) < 0.0001,
                FilterOperator.NotEquals => Math.Abs(nv - nf) >= 0.0001,
                FilterOperator.GreaterThan => nv > nf,
                FilterOperator.LessThan => nv < nf,
                FilterOperator.GreaterThanOrEqual => nv >= nf,
                FilterOperator.LessThanOrEqual => nv <= nf,
                _ => Math.Abs(nv - nf) < 0.0001
            };
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            if (raw is DateTime dt && DateTime.TryParse(filter.Value, CultureInfo.InvariantCulture, out DateTime df))
            {
                return filter.Operator switch
                {
                    FilterOperator.Equals => dt.Date == df.Date,
                    FilterOperator.NotEquals => dt.Date != df.Date,
                    FilterOperator.GreaterThan => dt > df,
                    FilterOperator.LessThan => dt < df,
                    FilterOperator.GreaterThanOrEqual => dt >= df,
                    FilterOperator.LessThanOrEqual => dt <= df,
                    _ => dt.Date == df.Date
                };
            }
            return false;
        }

        if (type == typeof(bool))
        {
            return bool.TryParse(filter.Value, out bool bf) && (bool)raw == bf;
        }

        if (type.IsEnum)
        {
            return strVal.Equals(filter.Value, StringComparison.OrdinalIgnoreCase);
        }

        return filter.Operator switch
        {
            FilterOperator.Contains => strVal.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Equals => strVal.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !strVal.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => strVal.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => strVal.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            _ => strVal.Contains(filter.Value, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
               type == typeof(byte) || type == typeof(double) || type == typeof(float) ||
               type == typeof(decimal) || type == typeof(uint) || type == typeof(ulong);
    }

    private void OnGlobalSearchInput(ChangeEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        CancellationToken token = _searchDebounce.Token;
        string value = e.Value?.ToString() ?? "";

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                await InvokeAsync(() =>
                {
                    _globalSearch = value;
                    _currentPage = 1;
                    if (ServerMode) _ = NotifyStateChangedAsync();
                    else ProcessData();
                    StateHasChanged();
                });
            }
            catch (TaskCanceledException)
            {
                // Debounce cancelled — expected when user types quickly
            }
        }, token);
    }

    private void OnColumnFilterChanged(string propertyName, ChangeEventArgs e)
    {
        string value = e.Value?.ToString() ?? "";
        GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == propertyName);
        if (col is null) return;

        Type type = Nullable.GetUnderlyingType(col.PropertyType) ?? col.PropertyType;
        FilterOperator op = (IsNumericType(type) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(bool) || type.IsEnum)
            ? FilterOperator.Equals
            : FilterOperator.Contains;

        if (string.IsNullOrWhiteSpace(value))
            _filters.Remove(propertyName);
        else
            _filters[propertyName] = new FilterDescriptor { PropertyName = propertyName, Value = value, Operator = op };

        _currentPage = 1;
        if (ServerMode) _ = NotifyStateChangedAsync();
        else ProcessData();
    }

    private void ClearAllFilters()
    {
        _globalSearch = "";
        _filters.Clear();
        _currentPage = 1;
        if (ServerMode) _ = NotifyStateChangedAsync();
        else ProcessData();
    }

    private void ClearSearch()
    {
        _globalSearch = "";
        _currentPage = 1;
        ProcessData();
    }

    private string GetFilterValue(string propName)
    {
        return _filters.TryGetValue(propName, out FilterDescriptor? f) ? f.Value : "";
    }

    private bool HasActiveFilters
        => _filters.Count > 0 || !string.IsNullOrWhiteSpace(_globalSearch);

    private int ActiveFilterCount
        => _filters.Count + (string.IsNullOrWhiteSpace(_globalSearch) ? 0 : 1);

    #endregion

    #region Sorting

    private List<TItem> ApplySorting(List<TItem> data)
    {
        if (_sorts.Count == 0) return [.. data];

        IOrderedEnumerable<TItem>? ordered = null;
        foreach (SortDescriptor sort in _sorts)
        {
            GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == sort.PropertyName);
            if (col is null) continue;

            Func<TItem, object?> key = col.ValueAccessor;

            #pragma warning disable S3358 // Ternary nesting required by IDE0045 simplification
                        ordered = ordered is null
                            ? (sort.Direction == SortDirection.Descending ? data.OrderByDescending(key) : data.OrderBy(key))
                            : (sort.Direction == SortDirection.Descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
            #pragma warning restore S3358
        }

        return ordered?.ToList() ?? [.. data];
    }

    private void OnColumnSort(string propertyName, bool multi)
    {
        SortDescriptor? existing = _sorts.Find(s => s.PropertyName == propertyName);

        if (!multi)
        {
            if (existing is not null)
            {
                if (existing.Direction == SortDirection.Ascending)
                {
                    _sorts.Clear();
                    _sorts.Add(new SortDescriptor { PropertyName = propertyName, Direction = SortDirection.Descending });
                }
                else
                {
                    _sorts.Clear();
                }
            }
            else
            {
                _sorts.Clear();
                _sorts.Add(new SortDescriptor { PropertyName = propertyName, Direction = SortDirection.Ascending });
            }
        }
        else
        {
            if (existing is not null)
            {
                if (existing.Direction == SortDirection.Ascending)
                    existing.Direction = SortDirection.Descending;
                else
                    _sorts.Remove(existing);
            }
            else
            {
                _sorts.Add(new SortDescriptor { PropertyName = propertyName, Direction = SortDirection.Ascending });
            }
        }

        if (ServerMode)
        {
            _currentPage = 1;
            _ = NotifyStateChangedAsync();
        }
        else
        {
            ProcessData();
        }
    }

    private SortDirection GetSortDir(string propName)
        => _sorts.Find(s => s.PropertyName == propName)?.Direction ?? SortDirection.None;

    private int GetSortIdx(string propName)
        => _sorts.FindIndex(s => s.PropertyName == propName);

    #endregion

    #region Grouping

    private List<GroupBucket<TItem>> BuildGroups(List<TItem> data, List<string> groupCols, int level)
    {
        if (level >= groupCols.Count) return [];

        string propName = groupCols[level];
        GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == propName);
        if (col is null) return [];

        return data
            .GroupBy(item => col.ValueAccessor(item)?.ToString() ?? L["Grid_GroupEmpty"])
            .Select(g => new GroupBucket<TItem>
            {
                Key = g.Key,
                KeyDisplay = g.Key?.ToString() ?? L["Grid_GroupEmpty"],
                PropertyName = propName,
                Level = level,
                Items = level == groupCols.Count - 1 ? g.ToList() : [],
                SubGroups = level < groupCols.Count - 1
                    ? BuildGroups(g.ToList(), groupCols, level + 1) : []
            })
            .ToList();
    }

    private List<DisplayRow<TItem>> FlattenGroups(List<GroupBucket<TItem>> groups)
    {
        List<DisplayRow<TItem>> rows = [];
        FlattenRecursive(groups, rows);
        return rows;
    }

    private void FlattenRecursive(List<GroupBucket<TItem>> groups, List<DisplayRow<TItem>> rows)
    {
        foreach (GroupBucket<TItem> g in groups)
        {
            string gid = $"{g.Level}_{g.Key}";
            bool collapsed = _collapsedGroups.Contains(gid);

            GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == g.PropertyName);

            List<TItem> allItems = GetAllGroupItems(g);
            var numCols = _columns
                .Where(c => c.Visible && IsNumericType(Nullable.GetUnderlyingType(c.PropertyType) ?? c.PropertyType))
                .ToList();

            double[] aggs = numCols.Select(nc =>
                allItems.Sum(item =>
                {
                    object? v = nc.ValueAccessor(item);
                    return v is not null && double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0;
                })).ToArray();

            rows.Add(new DisplayRow<TItem>
            {
                IsGroupHeader = true,
                GroupKey = g.KeyDisplay,
                GroupTitle = col?.Title ?? g.PropertyName,
                GroupCount = g.TotalCount,
                Level = g.Level,
                GroupId = gid,
                NumericAggregates = aggs
            });

            if (!collapsed)
            {
                if (g.SubGroups.Count > 0)
                    FlattenRecursive(g.SubGroups, rows);
                else
                    rows.AddRange(g.Items.Select(item => new DisplayRow<TItem>
                    {
                        IsGroupHeader = false,
                        Item = item,
                        Level = g.Level + 1
                    }));
            }
        }
    }

    private static List<TItem> GetAllGroupItems(GroupBucket<TItem> g)
    {
        if (g.SubGroups.Count > 0)
            return g.SubGroups.SelectMany(GetAllGroupItems).ToList();
        return g.Items;
    }

    private void ToggleGroupCollapse(string groupId)
    {
        if (!_collapsedGroups.Remove(groupId))
            _collapsedGroups.Add(groupId);
        ProcessData();
    }

    // Each of these notifies the parent as well as re-processing locally: in server
    // mode the parent is holding one page, and grouping over a page is not grouping
    // (see GridQueryState.GroupByColumns). Client mode ignores the callback.
    private async Task ToggleGroupColumn(string propName)
    {
        if (!_groupByColumns.Remove(propName))
            _groupByColumns.Add(propName);
        _collapsedGroups.Clear();
        ProcessData();
        if (ServerMode) await NotifyStateChangedAsync();
    }

    private async Task RemoveGroupColumn(string propName)
    {
        _groupByColumns.Remove(propName);
        _collapsedGroups.Clear();
        ProcessData();
        if (ServerMode) await NotifyStateChangedAsync();
    }

    private async Task ClearGroups()
    {
        _groupByColumns.Clear();
        _collapsedGroups.Clear();
        ProcessData();
        if (ServerMode) await NotifyStateChangedAsync();
    }

    #endregion

    #region Pagination

    private void ApplyPaging()
    {
        int skip = (_currentPage - 1) * _currentPageSize;
        _pagedData = _sortedData.Skip(skip).Take(_currentPageSize).ToList();
    }

    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        _currentPage = page;
        if (ServerMode) _ = NotifyStateChangedAsync();
        else ApplyPaging();
    }

    private void OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int size) && size > 0)
        {
            _currentPageSize = size;
            _currentPage = 1;
            if (ServerMode) _ = NotifyStateChangedAsync();
            else ProcessData();
        }
    }

    /// <summary>Fires <see cref="OnStateChanged"/> carrying the current grid query state.</summary>
    private Task NotifyStateChangedAsync()
    {
        if (!OnStateChanged.HasDelegate) return Task.CompletedTask;
        return OnStateChanged.InvokeAsync(new GridQueryState
        {
            Page          = _currentPage,
            PageSize      = _currentPageSize,
            SortProperty  = _sorts.FirstOrDefault()?.PropertyName,
            SortDirection = _sorts.FirstOrDefault()?.Direction ?? SortDirection.None,
            GlobalSearch  = string.IsNullOrWhiteSpace(_globalSearch) ? null : _globalSearch,
            ColumnFilters = _filters.ToDictionary(kv => kv.Key, kv => kv.Value.Value),
            GroupByColumns = [.. _groupByColumns]
        });
    }

    private List<int> PageNumbers()
    {
        int total = TotalPages;
        int cur = _currentPage;
        const int range = 2;

        List<int> pages = [1];

        int start = Math.Max(2, cur - range);
        int end = Math.Min(total - 1, cur + range);

        if (start > 2) pages.Add(-1);
        for (int i = start; i <= end; i++) pages.Add(i);
        if (end < total - 1) pages.Add(-1);
        if (total > 1) pages.Add(total);

        return pages;
    }

    private int FirstRowIndex => (_currentPage - 1) * _currentPageSize + 1;
    private int LastRowIndex => Math.Min(_currentPage * _currentPageSize, _totalFilteredCount);

    #endregion

    #region Selection

    private bool IsAllSelected => _pagedData.Count > 0 && _pagedData.All(_selectedItems.Contains);

    private void ToggleSelectAll()
    {
        if (IsAllSelected)
        {
            foreach (TItem item in _pagedData) _selectedItems.Remove(item);
        }
        else
        {
            foreach (TItem item in _pagedData) _selectedItems.Add(item);
        }
        OnSelectionChanged.InvokeAsync([.. _selectedItems]);
    }

    private void ToggleSelect(TItem item)
    {
        if (SelectionMode == SelectionMode.Row)
        {
            _selectedItems.Clear();
            _selectedItems.Add(item);
        }
        else
        {
            if (!_selectedItems.Remove(item))
                _selectedItems.Add(item);
        }
        OnSelectionChanged.InvokeAsync([.. _selectedItems]);
    }

    private bool IsSelected(TItem item) => _selectedItems.Contains(item);

    #endregion

    #region Row Detail

    private void ToggleRowDetail(int idx)
    {
        if (!_expandedDetailRows.Remove(idx))
            _expandedDetailRows.Add(idx);
    }

    #endregion

    #region Export

    private async Task ExportToCsv()
    {
        var exportCols = _columns.Where(c => c.Visible && c.Exportable).ToList();
        StringBuilder sb = new();

        sb.AppendLine(string.Join(";", exportCols.Select(c => Esc(c.Title))));

        foreach (TItem item in _filteredData)
            sb.AppendLine(string.Join(";", exportCols.Select(c => Esc(FormatValue(c, item)))));

        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] csv = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] content = [.. bom, .. csv];

        string name = string.IsNullOrWhiteSpace(Title) ? "export" : Title.Replace(" ", "_");
        await JS.InvokeVoidAsync("cmsJs.downloadFile",
            $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv", "text/csv;charset=utf-8", content);
    }

    private static string Esc(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return v.Contains('"') || v.Contains(';') || v.Contains('\n')
            ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    #endregion

    #region Import

    private async Task OnImportFile(InputFileChangeEventArgs e)
    {
        try
        {
            const long maxFileSize = 10 * 1024 * 1024; // 10 MB — reasonable for CSV grid import
#pragma warning disable S5693 // Content length verified with constant
            using StreamReader reader = new(e.File.OpenReadStream(maxFileSize));
#pragma warning restore S5693
            string content = await reader.ReadToEndAsync();
            string[] lines = content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return;

            string[] headers = ParseCsvLine(lines[0]);
            List<TItem> items = [];

            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = ParseCsvLine(lines[i]);
                TItem? item = TryCreateItem(headers, values);
                if (item is not null) items.Add(item);
            }

            if (items.Count > 0)
                await OnDataImported.InvokeAsync(items);
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger(GetType()).LogError(ex, "Grid import failed.");
        }
    }

    private TItem? TryCreateItem(string[] headers, string[] values)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length && i < values.Length; i++)
        {
            string h = headers[i].Trim();
            GridColumnDef<TItem>? col = _columns.Find(c =>
                c.Title.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                c.PropertyName.Equals(h, StringComparison.OrdinalIgnoreCase));
            map[col?.PropertyName ?? h] = values[i].Trim();
        }

        // Try parameterless constructor first
        try
        {
            TItem? instance = Activator.CreateInstance<TItem>();
            if (instance is not null)
            {
                foreach (PropertyInfo prop in typeof(TItem).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite || !map.TryGetValue(prop.Name, out string? raw)) continue;
                    object? converted = ConvertValue(raw, prop.PropertyType);
                    if (converted is not null) prop.SetValue(instance, converted);
                }
                return instance;
            }
        }
        catch (MissingMethodException)
        {
            // Parameterless constructor not available — try primary constructor below
        }

        // Try primary constructor (records)
        try
        {
            ConstructorInfo? ctor = typeof(TItem).GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor is null) return default;

            ParameterInfo[] parms = ctor.GetParameters();
            object?[] args = new object?[parms.Length];
            for (int i = 0; i < parms.Length; i++)
            {
                ParameterInfo p = parms[i];
                #pragma warning disable S3358 // Ternary nesting required by IDE0045 simplification
                                args[i] = map.TryGetValue(p.Name!, out string? raw)
                                    ? (ConvertValue(raw, p.ParameterType) ?? DefaultVal(p.ParameterType))
                                    : (p.HasDefaultValue ? p.DefaultValue : DefaultVal(p.ParameterType));
                #pragma warning restore S3358
            }
            return (TItem)ctor.Invoke(args);
        }
        catch { return default; }
    }

    private static object? ConvertValue(string value, Type target)
    {
        if (string.IsNullOrEmpty(value))
            return Nullable.GetUnderlyingType(target) is not null ? null : DefaultVal(target);

        Type t = Nullable.GetUnderlyingType(target) ?? target;
        try
        {
            if (t == typeof(string)) return value;
            if (t == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return bool.Parse(value);
            if (t == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
            if (t == typeof(Guid)) return Guid.Parse(value);
            if (t.IsEnum) return Enum.Parse(t, value, true);
            return Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
        }
        catch { return DefaultVal(target); }
    }

    private static object? DefaultVal(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

    private static string[] ParseCsvLine(string line)
    {
        List<string> result = [];
        bool inQ = false;
        StringBuilder cur = new();

        foreach (char c in line)
        {
            if (inQ)
            {
                if (c == '"') inQ = false;
                else cur.Append(c);
            }
            else
            {
                if (c == '"') inQ = true;
                else if (c == ';') { result.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
        }
        result.Add(cur.ToString());
        return [.. result];
    }

    #endregion

    #region Formatting

    private string FormatValue(GridColumnDef<TItem> col, TItem item)
    {
        object? value = col.ValueAccessor(item);
        if (value is null) return "";
        if (value is IEnumerable<string> list) return string.Join(", ", list);
        if (value is bool b) return b ? L["Grid_BoolTrue"] : L["Grid_BoolFalse"];
        if (!string.IsNullOrEmpty(col.Format) && value is IFormattable fmt)
            return fmt.ToString(col.Format, CultureInfo.CurrentCulture);
        return value.ToString() ?? "";
    }

    #endregion

    #region Column Visibility & Density

    private void ToggleColumnVisibility(GridColumnDef<TItem> col) => col.Visible = !col.Visible;

    private void CycleDensity()
    {
        _density = _density switch
        {
            GridDensity.Compact => GridDensity.Normal,
            GridDensity.Normal => GridDensity.Comfortable,
            _ => GridDensity.Compact
        };
    }

    #endregion

    #region Helpers

    private string DensityClass => _density switch
    {
        GridDensity.Compact => "eg-compact",
        GridDensity.Comfortable => "eg-comfortable",
        _ => ""
    };

    private string DensityIcon => _density switch
    {
        GridDensity.Compact => "bi-list",
        GridDensity.Comfortable => "bi-grid-3x3-gap",
        _ => "bi-list-ul"
    };

    private string TableClasses
    {
        get
        {
            StringBuilder sb = new("eg-table");
            if (Striped) sb.Append(" eg-striped");
            if (Hover) sb.Append(" eg-hover");
            if (Bordered) sb.Append(" eg-bordered");
            return sb.ToString();
        }
    }

    private static string AlignCls(TextAlign a) => a switch
    {
        TextAlign.Center => "eg-center",
        TextAlign.Right => "eg-right",
        _ => ""
    };

    private static string SortIcon(SortDirection d) => d switch
    {
        SortDirection.Ascending => "bi-sort-up",
        SortDirection.Descending => "bi-sort-down",
        _ => "bi-arrow-down-up"
    };

    private static bool IsBoolType(Type t) => (Nullable.GetUnderlyingType(t) ?? t) == typeof(bool);
    private static bool IsEnumType(Type t) => (Nullable.GetUnderlyingType(t) ?? t).IsEnum;
    private static bool IsDateType(Type t)
    {
        Type bt = Nullable.GetUnderlyingType(t) ?? t;
        return bt == typeof(DateTime) || bt == typeof(DateTimeOffset);
    }
    private static Type BaseType(Type t) => Nullable.GetUnderlyingType(t) ?? t;

    private static object? ParseDateOrDefault(string? value, object? fallback)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
            return d;
        return fallback;
    }

    #endregion

    #region Inline Editing

    private bool HasPendingChanges => _pendingEdits.Count > 0;
    private int PendingChangeCount => _pendingEdits.Count;

    private bool IsEditing(TItem item) => _editingRows.Contains(item);
    private bool IsRowModified(TItem item) => _pendingEdits.ContainsKey(item);

    // Opening a row for editing changes nothing yet: it counts as a pending change
    // only once a value actually differs (see SetEditValue). Recording every opened
    // row made "2 changes" appear for rows nobody had touched.
    private void StartRowEdit(TItem item)
    {
        if (!AllowInlineEdit) return;
        _editingRows.Add(item);
    }

    private void FinishRowEdit(TItem item)
    {
        _editingRows.Remove(item);
    }

    private void CancelRowEdit(TItem item)
    {
        _editingRows.Remove(item);
        _pendingEdits.Remove(item);
    }

    private void SetEditValue(TItem item, string propertyName, object? value)
    {
        object? original = _columns.Find(c => c.PropertyName == propertyName)?.ValueAccessor(item);
        bool unchanged = Equals(original, value)
            || string.Equals(original?.ToString(), value?.ToString(), StringComparison.Ordinal);

        if (!_pendingEdits.TryGetValue(item, out Dictionary<string, object?>? values))
        {
            if (unchanged) return;
            values = new Dictionary<string, object?>();
            _pendingEdits[item] = values;
        }

        if (unchanged)
        {
            values.Remove(propertyName);
            if (values.Count == 0) _pendingEdits.Remove(item);
            return;
        }
        values[propertyName] = value;
    }

    private object? GetEditValue(TItem item, string propertyName)
    {
        if (_pendingEdits.TryGetValue(item, out Dictionary<string, object?>? values) && values.TryGetValue(propertyName, out object? val))
            return val;
        GridColumnDef<TItem>? col = _columns.Find(c => c.PropertyName == propertyName);
        return col?.ValueAccessor(item);
    }

    private async Task SaveAllChanges()
    {
        if (!OnSaveChanges.HasDelegate) return;
        var edits = _pendingEdits.Select(kv => new EditedRow<TItem>(kv.Key, kv.Value)).ToList();
        await OnSaveChanges.InvokeAsync(edits);
        _editingRows.Clear();
        _pendingEdits.Clear();
    }

    private async Task DiscardAllChanges()
    {
        _editingRows.Clear();
        _pendingEdits.Clear();
        if (OnDiscardChanges.HasDelegate)
            await OnDiscardChanges.InvokeAsync();
    }

    #endregion

    #region Public API

    public void Refresh()
    {
        ProcessData();
        StateHasChanged();
    }

    #endregion
}
