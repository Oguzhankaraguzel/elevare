using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteSettings.TestCdn;

internal sealed class TestCdnCommandHandler(ICdnProbe probe)
    : ICommandHandler<TestCdnCommand, CdnProbeResult>
{
    public Task<Result<CdnProbeResult>> Handle(TestCdnCommand request, CancellationToken cancellationToken)
        => probe.CheckAsync(request.CdnBaseUrl, cancellationToken);
}
