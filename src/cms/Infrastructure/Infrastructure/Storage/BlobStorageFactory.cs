using Application.Abstraction.Services.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>
/// Singleton — cheap, holds nothing but a reference to the options monitor.
/// Reads <c>ObjectStorage:Provider</c> live on every <see cref="Create"/> call
/// rather than once at startup, which is what lets a Sırlar save switch Local
/// disk ↔ S3 without a restart: both <see cref="LocalDiskBlobStorage"/> and
/// <see cref="S3BlobStorage"/> stay registered at all times (see
/// <c>InfrastructureServiceRegistration</c>), and this factory just points the
/// caller at whichever one the current setting names.
/// <para>
/// Deliberately does NOT hold its own <c>IServiceProvider</c> — see
/// <see cref="IBlobStorageFactory.Create"/>'s doc comment for why that would be
/// a captive-dependency bug for a singleton resolving Scoped services.
/// </para>
/// </summary>
internal sealed class BlobStorageFactory(IOptionsMonitor<ObjectStorageOptions> optionsMonitor) : IBlobStorageFactory
{
    public IBlobStorage Create(IServiceProvider scopedProvider) =>
        string.Equals(optionsMonitor.CurrentValue.Provider, "S3", StringComparison.OrdinalIgnoreCase)
            ? scopedProvider.GetRequiredService<S3BlobStorage>()
            : scopedProvider.GetRequiredService<LocalDiskBlobStorage>();
}
