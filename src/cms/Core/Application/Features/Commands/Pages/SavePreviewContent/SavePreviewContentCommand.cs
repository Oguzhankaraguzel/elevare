using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.SavePreviewContent;

/// <summary>
/// Stages the editor's current (possibly unsaved) GrapeJS output for the "preview
/// without publishing" flow. Unlike <see cref="Application.Features.Commands.Pages.UpdatePage.UpdatePageCommand"/>,
/// this touches only the preview snapshot columns — title, slug, status, SEO and
/// parent are left untouched, so previewing an already-Published page never
/// affects what real visitors see.
/// </summary>
public sealed record SavePreviewContentCommand(int PageId, string? GjsHtml, string? GjsCss) : ICommand;
