namespace Application.Features.Trash;

/// <summary>Every soft-deletable entity type surfaced in the admin Trash page.</summary>
public enum TrashEntityType
{
    PageInfo = 0,
    PageTemplate = 1,
    MediaFile = 2,
    Language = 3,
    SiteSetting = 4,
    UserTask = 5,
    UserNote = 6,
    UserReminder = 7,
    ContentBulkEdit = 8,
    SiteCodeSnippet = 9,
}
