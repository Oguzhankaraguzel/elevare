namespace Wasm.Components.Shared.Grid;

public sealed class DisplayRow<TItem>
{
    public required bool IsGroupHeader { get; init; }
    public TItem? Item { get; init; }
    public string GroupKey { get; init; } = "";
    public string GroupTitle { get; init; } = "";
    public int GroupCount { get; init; }
    public int Level { get; init; }
    public string GroupId { get; init; } = "";
    public double[] NumericAggregates { get; init; } = [];
}
