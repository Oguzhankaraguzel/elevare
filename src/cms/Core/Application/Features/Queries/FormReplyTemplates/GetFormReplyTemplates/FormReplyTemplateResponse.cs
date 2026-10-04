namespace Application.Features.Queries.FormReplyTemplates.GetFormReplyTemplates;

public sealed record FormReplyTemplateResponse(
    int Id,
    string Name,
    string Subject,
    string Body,
    bool IsActive,
    int SortOrder);
