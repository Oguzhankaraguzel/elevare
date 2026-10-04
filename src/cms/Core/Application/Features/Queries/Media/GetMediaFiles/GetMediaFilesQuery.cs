using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Media;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Media.GetMediaFiles;

/// <summary>
/// Deliberately open to any signed-in user, like the page and template lists.
/// <para>
/// Browsing the library is part of editing a page — the Developer role, for one,
/// holds <c>Pages.Edit</c> without <c>Media.Download</c>, so gating this would
/// leave it unable to pick an image for a page it is allowed to edit. Uploading,
/// renaming and deleting media are the actions that carry weight, and those
/// commands declare their permissions.
/// </para>
/// </summary>
/// <param name="SortBy">
/// Column to order by — <c>FileName</c>, <c>FileSize</c>, <c>MediaType</c> or
/// <c>CreateDate</c>. Anything else (including null) falls back to newest-first.
/// Sorting has to happen here rather than in the grid: the grid only ever holds one
/// page, so sorting client-side would reorder 24 rows out of however many hundred
/// the library actually has and call it sorted.
/// </param>
public sealed record GetMediaFilesQuery(
    string? Search = null,
    MediaType? MediaType = null,
    int Page = 1,
    int PageSize = 30,
    string? SortBy = null,
    bool SortDescending = true) : IQuery<PagedResult<MediaFileResponse>>;
