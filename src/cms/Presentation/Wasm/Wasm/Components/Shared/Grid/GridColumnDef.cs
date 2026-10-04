using Microsoft.AspNetCore.Components;

namespace Wasm.Components.Shared.Grid;

public sealed class GridColumnDef<TItem>
{
    public required string PropertyName { get; init; }
    public string Title { get; set; } = "";
    public Type PropertyType { get; init; } = typeof(string);
    public required Func<TItem, object?> ValueAccessor { get; init; }
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = true;
    public bool Groupable { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Exportable { get; set; } = true;
    public string? Width { get; set; }
    public string? Format { get; set; }
    public TextAlign Align { get; set; } = TextAlign.Left;
    public RenderFragment<TItem>? CellTemplate { get; set; }
    /// <summary>When true this column will be rendered as sticky (CSS position:sticky) on the left.</summary>
    public bool Sticky { get; set; }
    /// <summary>When true this column can be edited inline.</summary>
    public bool Editable { get; set; }
    /// <summary>Optional custom edit template. When null the grid auto-generates an input based on PropertyType.</summary>
    public RenderFragment<CellEditContext<TItem>>? EditTemplate { get; set; }
}
