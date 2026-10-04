namespace Wasm.Components.Shared.Grid;

public sealed record EditedRow<TItem>(TItem Item, IReadOnlyDictionary<string, object?> EditedValues);
