namespace Domain.Entities.Redirects;

public enum RedirectReason
{
    Manual = 0,
    SlugChanged = 1,
    PageDeleted = 2,
    PageArchived = 3,
}
