namespace Application.Features.Queries.PageTemplates.GetPageTemplateVersions;

/// <summary>The template's display name and its last-touched moment, keyed by id on the client.</summary>
public sealed record PageTemplateVersionInfo(string Name, DateTime Version);
