using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Announcements.GetAnnouncements;

/// <summary>
/// Announcements are just notes shared with everyone, so anyone signed in may read
/// them — no permission key.
/// </summary>
/// <param name="Take">How many to return, newest first. The bell shows a handful.</param>
public sealed record GetAnnouncementsQuery(int Take = 10) : IQuery<AnnouncementListResponse>;
