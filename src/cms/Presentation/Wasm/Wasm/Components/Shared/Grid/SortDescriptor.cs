namespace Wasm.Components.Shared.Grid;

public sealed class SortDescriptor
{
    public required string PropertyName { get; init; }
    public SortDirection Direction { get; set; }
}
