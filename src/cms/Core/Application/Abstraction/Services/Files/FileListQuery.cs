using Domain.Entities.Media;

namespace Application.Abstraction.Services.Files;

/// <summary>Parameters for paged, filtered file listing.</summary>
public sealed class FileListQuery
{
    /// <summary>Filter by media type; <c>null</c> returns all types.</summary>
    public MediaType? MediaType { get; init; }

    /// <summary>Filter by logical folder path prefix (e.g. <c>uploads/images/2025</c>).</summary>
    public string? FolderPath { get; init; }

    /// <summary>
    /// Case-insensitive substring match against <c>OriginalFileName</c> and <c>Title</c>.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page. Defaults to 24 (3 × 8 grid).</summary>
    public int PageSize { get; init; } = 24;
}
