using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicLanguages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Concrete;
using System.Data.Common;

namespace Application.Services;

/// <summary>
/// Singleton cache backing <see cref="ILanguageDirectory"/>. Starts with a safe
/// fallback (only "tr" known/default) until the first refresh completes; a failed
/// refresh keeps the previous snapshot rather than clearing it, so a transient DB
/// outage doesn't suddenly make every language route 404.
/// </summary>
internal sealed class LanguageDirectory(IServiceScopeFactory scopeFactory) : ILanguageDirectory
{
    private HashSet<string> _codes = new(StringComparer.OrdinalIgnoreCase) { "tr" };

    public bool IsKnownLanguageCode(string code) => _codes.Contains(code);

    public string DefaultLanguageCode { get; private set; } = "tr";

    public async Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IPublicReadDbContext db = scope.ServiceProvider.GetRequiredService<IPublicReadDbContext>();

            List<PublicLanguage> languages = await db.Languages
                .Where(PublicLanguage.PubliclyVisible)
                .ToListAsync(cancellationToken);

            // No published languages at all is treated as "keep what we have": it is far
            // more likely to be a half-finished migration than a real intent to make
            // every language route 404.
            if (languages.Count == 0)
                return Result.Success();

            _codes = new HashSet<string>(languages.Select(l => l.TwoLetterCode), StringComparer.OrdinalIgnoreCase);
            DefaultLanguageCode = languages.FirstOrDefault(l => l.IsDefault)?.TwoLetterCode ?? languages[0].TwoLetterCode;

            return Result.Success();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            // The previous snapshot is deliberately left in place — see the class remarks.
            return Result.Failure(SiteStateErrors.RefreshFailed("language", ex.Message));
        }
    }
}
