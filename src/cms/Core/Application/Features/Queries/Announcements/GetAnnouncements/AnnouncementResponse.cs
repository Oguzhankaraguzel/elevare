namespace Application.Features.Queries.Announcements.GetAnnouncements;

public sealed record AnnouncementResponse(
    int Id,
    string Title,
    string Content,
    string? Color,
    bool IsPinned,
    string CreatedBy,
    DateTime CreateDate,
    bool IsRead);
