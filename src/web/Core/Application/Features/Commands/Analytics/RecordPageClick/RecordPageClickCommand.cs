using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Analytics.RecordPageClick;

/// <summary>Appends one click-tracking row for a <c>data-track-click</c>-marked element.</summary>
public sealed record RecordPageClickCommand(string Path, string ElementLabel, Guid VisitorId) : ICommand;
