namespace Application.Abstraction.Services.Storage;

/// <summary>
/// Resolves the <see cref="IBlobStorage"/> implementation that matches the
/// CURRENT <c>ObjectStorage:Provider</c> value — read live on every call via
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/>, rather
/// than a type picked once at DI-registration time. This is what lets switching
/// Local disk ↔ S3 from the "Sırlar" screen take effect without a restart: both
/// implementations stay registered at all times, and this factory just points
/// callers at whichever one the saved setting currently names.
/// </summary>
public interface IBlobStorageFactory
{
    /// <summary>
    /// <paramref name="scopedProvider"/> must be the CALLER's own (request/circuit)
    /// scope, not a provider this factory holds itself — <c>S3BlobStorage</c> and
    /// <c>LocalDiskBlobStorage</c> are registered Scoped (the latter depends on the
    /// request's own DbContext), and this factory is a singleton. Resolving them
    /// from a provider captured by a singleton would silently pull them out of the
    /// DI container's root scope instead, turning them into de-facto singletons
    /// with a stale, shared DbContext — exactly the bug this parameter avoids.
    /// </summary>
    IBlobStorage Create(IServiceProvider scopedProvider);
}
