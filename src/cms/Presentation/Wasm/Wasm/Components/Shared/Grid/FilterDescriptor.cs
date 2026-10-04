namespace Wasm.Components.Shared.Grid;

public sealed class FilterDescriptor
{
    public required string PropertyName { get; init; }
    public FilterOperator Operator { get; set; } = FilterOperator.Contains;
    public string Value { get; set; } = "";
}
