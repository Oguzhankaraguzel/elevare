using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Analytics.GetMilestones;

/// <param name="Take">How many to show. This is a garnish, not a report.</param>
public sealed record GetMilestonesQuery(int Take = 4) : IQuery<List<MilestoneResponse>>;
