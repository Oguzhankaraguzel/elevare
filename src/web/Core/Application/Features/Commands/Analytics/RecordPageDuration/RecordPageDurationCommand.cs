using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Analytics.RecordPageDuration;

/// <summary>Reports the seconds a visitor spent on a previously recorded page view
/// (sent by the browser via a beacon when the visitor leaves the page).</summary>
public sealed record RecordPageDurationCommand(long HitId, int Seconds) : ICommand;
