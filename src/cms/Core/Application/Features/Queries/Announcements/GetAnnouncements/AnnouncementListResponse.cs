namespace Application.Features.Queries.Announcements.GetAnnouncements;

/// <param name="UnreadCount">
/// Counts every unread announcement, not just the ones in <paramref name="Items"/> —
/// the badge must not say "3" when there are eleven waiting behind the cut-off.
/// </param>
public sealed record AnnouncementListResponse(int UnreadCount, IReadOnlyList<AnnouncementResponse> Items);
