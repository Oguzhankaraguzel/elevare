using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetDeletedTranslation;

/// <summary>
/// Is there a trashed version of this page in the target language? Asked before
/// creating a translation, so "I deleted it a minute ago and want it back" and
/// "I want to start over" stop being the same button.
/// </summary>
public sealed record GetDeletedTranslationQuery(int SourcePageId, int TargetLanguageId)
    : IQuery<DeletedTranslationResponse?>;
