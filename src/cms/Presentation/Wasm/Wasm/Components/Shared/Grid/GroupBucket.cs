namespace Wasm.Components.Shared.Grid;

public sealed class GroupBucket<TItem>
{
    public required object? Key { get; init; }
    public required string KeyDisplay { get; init; }
    public required string PropertyName { get; init; }
    public required int Level { get; init; }
    public List<TItem> Items { get; init; } = [];
    public List<GroupBucket<TItem>> SubGroups { get; init; } = [];
    public int TotalCount => SubGroups.Count > 0 ? SubGroups.Sum(g => g.TotalCount) : Items.Count;
}
