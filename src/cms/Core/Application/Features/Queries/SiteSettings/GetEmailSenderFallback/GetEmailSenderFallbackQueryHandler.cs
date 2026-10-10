using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetEmailSenderFallback;

internal sealed class GetEmailSenderFallbackQueryHandler(IConfigurationInspector configuration)
    : IQueryHandler<GetEmailSenderFallbackQuery, EmailSenderFallbackResponse>
{
    public Task<Result<EmailSenderFallbackResponse>> Handle(GetEmailSenderFallbackQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new EmailSenderFallbackResponse(
            configuration.GetEffectiveValue("Email:FromAddress"),
            configuration.GetEffectiveValue("Email:FromDisplayName"))));
}
