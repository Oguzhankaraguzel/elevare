namespace Wasm.Components.Services;

/// <summary>Singleton service — inject into components to show toast notifications.</summary>
public sealed class ToastService
{
    private readonly Lock _lock = new();
    private readonly List<ToastMessage> _messages = [];
    public IReadOnlyList<ToastMessage> Messages { get { lock (_lock) { return [.. _messages]; } } }

#pragma warning disable CA1003  // Using Action for Blazor StateHasChanged compatibility
    public event Func<Task>? OnChange;
#pragma warning restore CA1003

    public void Show(string text, ToastType type = ToastType.Success, int durationMs = 3500)
    {
        ToastMessage msg = new(text, type, durationMs);
        lock (_lock) { _messages.Add(msg); }
        _ = NotifyChangedAsync();
        _ = RemoveAfterDelayAsync(msg, durationMs);
    }

    private async Task RemoveAfterDelayAsync(ToastMessage msg, int durationMs)
    {
        await Task.Delay(durationMs);
        lock (_lock) { _messages.Remove(msg); }
        _ = NotifyChangedAsync();
    }

    public void Success(string text) => Show(text, ToastType.Success);
    public void Error(string text) => Show(text, ToastType.Error, 5000);
    public void Warning(string text) => Show(text, ToastType.Warning, 4000);
    public void Info(string text) => Show(text, ToastType.Info);

    private Task NotifyChangedAsync() => OnChange?.Invoke() ?? Task.CompletedTask;
}
