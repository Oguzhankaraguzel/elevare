namespace Wasm.Components.Services;

/// <summary>
/// Scoped service to control the full-screen loading modal (<see cref="Wasm.Components.Shared.CmsLoadingModal"/>)
/// during long-running operations. Inject and call <see cref="Show"/> / <see cref="Hide"/> from any component.
/// </summary>
public sealed class LoadingService
{
    public bool IsVisible { get; private set; }
    public string? Message { get; private set; }

#pragma warning disable CA1003
    public event Func<Task>? OnChange;
#pragma warning restore CA1003

    /// <summary>Shows the loading modal with an optional custom message.</summary>
    public void Show(string? message = null)
    {
        IsVisible = true;
        Message = message;
        _ = NotifyAsync();
    }

    /// <summary>Hides the loading modal.</summary>
    public void Hide()
    {
        IsVisible = false;
        Message = null;
        _ = NotifyAsync();
    }

    private Task NotifyAsync() => OnChange?.Invoke() ?? Task.CompletedTask;
}
