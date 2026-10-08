using Application.Features.Queries.Redirects.GetRedirectTarget;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicRedirects;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// Redirects are what stop a renamed page from turning every existing link, search
/// result and bookmark into a 404, so the rules here are worth pinning down: which
/// key a request is looked up under, and which of the two possible targets wins.
/// </summary>
public sealed class GetRedirectTargetQueryHandlerTests
{
    private static async Task<PublicReadDbContext> SeedAsync(
        Action<PublicReadDbContext> arrange, string defaultCode = "tr")
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seed = TestDbFactory.Create(options))
        {
            seed.Languages.Add(new PublicLanguage
            {
                Id = 1,
                TwoLetterCode = defaultCode,
                IsDefault = true,
                IsActive = true,
                IsPublished = true,
            });
            arrange(seed);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        return TestDbFactory.Create(options);
    }

    [Fact]
    public async Task Default_language_looks_up_the_bare_slug()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "eski-sayfa", NewPath = "/yeni-sayfa" }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "eski-sayfa"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Url.ShouldBe("/yeni-sayfa");
    }

    [Fact]
    public async Task Non_default_language_looks_up_the_prefixed_slug()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "en/old-page", NewPath = "/en/new-page" }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("en", "old-page"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Url.ShouldBe("/en/new-page");
    }

    [Fact]
    public async Task A_live_source_page_wins_over_the_frozen_snapshot()
    {
        // The whole point of tracking SourcePageId: a page renamed twice must still
        // resolve to where it is now, not to where the first rename pointed.
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.PageInfos.Add(new PublicPage
            {
                Id = 7,
                Slug = "ucuncu-ad",
                FullSlug = "ucuncu-ad",
                LanguageId = 1,
                PageStatus = PublicPageStatus.Published,
                IsActive = true,
            });
            s.Redirects.Add(new PublicRedirect
            {
                Id = 1,
                OldPath = "ilk-ad",
                NewPath = "/ikinci-ad",
                SourcePageId = 7,
            });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "ilk-ad"), CancellationToken.None);

        result.Value.Url.ShouldBe("/ucuncu-ad");
    }

    [Fact]
    public async Task Unpublished_source_page_falls_back_to_the_snapshot()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.PageInfos.Add(new PublicPage
            {
                Id = 7,
                Slug = "gizlenmis",
                FullSlug = "gizlenmis",
                LanguageId = 1,
                PageStatus = PublicPageStatus.Draft,
                IsActive = true,
            });
            s.Redirects.Add(new PublicRedirect
            {
                Id = 1,
                OldPath = "ilk-ad",
                NewPath = "/elle-verilen-hedef",
                SourcePageId = 7,
            });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "ilk-ad"), CancellationToken.None);

        result.Value.Url.ShouldBe("/elle-verilen-hedef");
    }

    [Fact]
    public async Task A_rule_with_no_resolvable_target_fails_rather_than_redirecting_nowhere()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "ilk-ad", NewPath = null, SourcePageId = null }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "ilk-ad"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Redirect.NoTarget");
    }

    [Fact]
    public async Task An_unknown_path_reports_not_found_so_the_caller_can_serve_404()
    {
        using PublicReadDbContext db = await SeedAsync(_ => { });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "hic-var-olmayan"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Redirect.NotFound");
    }

    [Fact]
    public async Task A_chain_of_rules_collapses_into_a_single_final_target()
    {
        // zincir-a -> zincir-b -> /son-durak: a visitor hitting zincir-a must get ONE
        // 301 straight to /son-durak, not a 301 to zincir-b that then 301s again.
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "zincir-a", NewPath = "/zincir-b" });
            s.Redirects.Add(new PublicRedirect { Id = 2, OldPath = "zincir-b", NewPath = "/son-durak" });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "zincir-a"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Url.ShouldBe("/son-durak");
    }

    [Fact]
    public async Task Two_rules_pointing_at_each_other_fail_instead_of_redirecting_forever()
    {
        // dongu-x -> dongu-y -> dongu-x: nothing but an infinite 301 loop can come
        // out of this, so it must fail closed rather than hand back either target.
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "dongu-x", NewPath = "/dongu-y" });
            s.Redirects.Add(new PublicRedirect { Id = 2, OldPath = "dongu-y", NewPath = "/dongu-x" });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "dongu-x"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Redirect.Loop");
    }

    [Fact]
    public async Task A_rule_that_redirects_to_itself_fails_instead_of_redirecting_forever()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "kendine-donen", NewPath = "/kendine-donen" }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "kendine-donen"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Redirect.Loop");
    }

    [Fact]
    public async Task A_chain_that_ends_at_an_external_url_stops_walking_and_succeeds()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "eski-kampanya", NewPath = "/kampanya" });
            s.Redirects.Add(new PublicRedirect { Id = 2, OldPath = "kampanya", NewPath = "https://ornek.com/promo" });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "eski-kampanya"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Url.ShouldBe("https://ornek.com/promo");
    }

    [Fact]
    public async Task An_ordinary_rule_is_permanent()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "eski-sayfa", NewPath = "/yeni-sayfa" }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "eski-sayfa"), CancellationToken.None);

        result.Value.IsPermanent.ShouldBeTrue();
    }

    [Fact]
    public async Task A_temporary_rule_answers_302()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "en/about", NewPath = "/hakkimda", IsTemporary = true }));

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "en/about"), CancellationToken.None);

        result.Value.Url.ShouldBe("/hakkimda");
        result.Value.IsPermanent.ShouldBeFalse();
    }

    // An old English slug (permanent, from a rename) now leads into an unpublished
    // language whose pages redirect temporarily: the chain as a whole is not settled,
    // so it must not be announced as a permanent move.
    [Fact]
    public async Task One_temporary_hop_makes_the_whole_chain_temporary()
    {
        using PublicReadDbContext db = await SeedAsync(s =>
        {
            s.Redirects.Add(new PublicRedirect { Id = 1, OldPath = "en/old-about", NewPath = "/en/about" });
            s.Redirects.Add(new PublicRedirect { Id = 2, OldPath = "en/about", NewPath = "/hakkimda", IsTemporary = true });
        });

        Result<RedirectTarget> result = await new GetRedirectTargetQueryHandler(db)
            .Handle(new GetRedirectTargetQuery("tr", "en/old-about"), CancellationToken.None);

        result.Value.Url.ShouldBe("/hakkimda");
        result.Value.IsPermanent.ShouldBeFalse();
    }
}
