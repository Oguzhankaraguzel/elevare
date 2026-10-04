namespace Wasm.Components.Services;

public sealed record ToastMessage(string Text, ToastType Type = ToastType.Success, int DurationMs = 3500)
{
    public string Id { get; } = Guid.NewGuid().ToString();
}
