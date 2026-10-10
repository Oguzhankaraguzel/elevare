using Application.Abstraction.Services.Email;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetEmailStatus;

internal sealed class GetEmailStatusQueryHandler(IEmailService emailService) : IQueryHandler<GetEmailStatusQuery, bool>
{
    public async Task<Result<bool>> Handle(GetEmailStatusQuery request, CancellationToken cancellationToken) =>
        Result.Success(await emailService.IsConfiguredAsync(cancellationToken));
}
