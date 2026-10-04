using System.Reflection;
using Application.Marker;
using Domain.Marker;
using Infrastructure.Marker;
using Persistence.Marker;
using Wasm.Marker;

namespace Cms.Tests;

/// <summary>
/// Base class for cms architecture tests.
/// </summary>
public abstract class BaseTest
{
    protected static readonly Assembly DomainAssembly = typeof(ICmsDomain).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICmsApplication).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ICmsInfrastructure).Assembly;
    protected static readonly Assembly PersistenceAssembly = typeof(ICmsPersistence).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(ICmsWasm).Assembly;
}
