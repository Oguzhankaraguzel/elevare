using Domain.Entities.StructuredData;
using FluentValidation;

namespace Application.Features.Commands.Pages.UpdatePage;

internal sealed class UpdatePageCommandValidator : AbstractValidator<UpdatePageCommand>
{
    public UpdatePageCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ParentPageId).GreaterThan(0).When(x => x.ParentPageId.HasValue);

        RuleFor(x => x.Seo).NotNull();
        RuleFor(x => x.Seo.MetaDescription).MaximumLength(320).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.MetaAuthor).MaximumLength(200).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.FocusKeyword).MaximumLength(150).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.CanonicalUrl).MaximumLength(500).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.OgTitle).MaximumLength(200).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.OgDescription).MaximumLength(320).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.OgType).MaximumLength(50).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.OgImage).MaximumLength(500).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.OgUrl).MaximumLength(500).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.TwitterCard).MaximumLength(50).When(x => x.Seo is not null);
        RuleFor(x => x.Seo.TwitterSite).MaximumLength(100).When(x => x.Seo is not null);
        // No length cap: the column is nvarchar(max), and a realistic graph
        // (Organization + WebSite + WebPage + BreadcrumbList + Article) already runs
        // past the 5000 characters this used to allow — the builder would have
        // produced output it could not save.
        RuleFor(x => x.Seo.StructuredData)
            .Must(BeParseableJson)
            .WithErrorCode("Page.StructuredDataInvalidJson")
            .WithMessage("The structured data is not valid JSON. Published as it stands, no search engine can read any of it.")
            .When(x => x.Seo is not null);
    }

    /// <summary>
    /// Blocks the save on malformed JSON and nothing else. Completeness is checked
    /// separately and only ever warns, because schema.org has no required
    /// properties — but broken JSON is silently unreadable to every consumer, and
    /// nothing downstream would ever report it.
    /// </summary>
    private static bool BeParseableJson(string? structuredData) =>
        string.IsNullOrWhiteSpace(structuredData) || SchemaGraph.TryParse(structuredData, out _);
}
