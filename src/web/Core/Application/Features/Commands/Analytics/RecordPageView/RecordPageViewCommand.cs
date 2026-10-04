using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Analytics.RecordPageView;

/// <summary>Appends one page-view row; returns the new hit id so the browser can
/// report the time-on-page for it later (see RecordPageDurationCommand).</summary>
public sealed record RecordPageViewCommand(string Path, string? Title, Guid VisitorId) : ICommand<long>;
