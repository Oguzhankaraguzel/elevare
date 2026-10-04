namespace Wasm.Components.Shared.Grid;

public sealed class CellEditContext<TItem>
{
    public required TItem Item { get; init; }
    public required string PropertyName { get; init; }
    public object? Value { get; set; }
    public required Action<object?> ValueChanged { get; init; }
}
