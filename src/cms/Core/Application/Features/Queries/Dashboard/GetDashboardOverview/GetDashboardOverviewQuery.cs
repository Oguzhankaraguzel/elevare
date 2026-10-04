using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

/// <summary>
/// Personalized dashboard data for the current user: their task load, upcoming
/// reminders, notes, and a site-wide content-health breakdown.
/// </summary>
public sealed record GetDashboardOverviewQuery : IQuery<DashboardOverviewResponse>;
