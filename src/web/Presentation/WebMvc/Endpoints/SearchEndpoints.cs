using Application.Abstraction.Services;
using Application.Features.Queries.Search.SearchPublicContent;
using MediatR;
using SharedKernel.Concrete;
using WebMvc.RateLimiting;

namespace WebMvc.Endpoints;

/// <summary>
/// Backs the "Arama Kutusu" GrapeJS block's live search — called on every
/// debounced keystroke from elevare-interactions.js. Read-only, scoped to
/// Published pages only (see <see cref="SearchPublicContentQueryHandler"/>).
/// </summary>
public static class SearchEndpoints
{
    public static WebApplication MapSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/search", async (
            string? q, string? lang, int? max, ISender sender, ILanguageDirectory languageDirectory, CancellationToken ct) =>
        {
            string term = (q ?? "").Trim();
            if (term.Length < 2)
                return Results.Ok(Array.Empty<SearchResultItemResponse>());

            string languageCode = !string.IsNullOrWhiteSpace(lang) && languageDirectory.IsKnownLanguageCode(lang)
                ? lang
                : languageDirectory.DefaultLanguageCode;
            int maxResults = Math.Clamp(max ?? 12, 1, 50);

            Result<List<SearchResultItemResponse>> result = await sender.Send(
                new SearchPublicContentQuery(term, languageCode, maxResults), ct);

            return Results.Ok(result.IsSuccess ? result.Value : []);
        }).RequireRateLimiting(RateLimitPolicies.Search);

        return app;
    }
}
