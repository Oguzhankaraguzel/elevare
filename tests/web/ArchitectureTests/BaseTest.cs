using System.Reflection;
using Application.Marker;
using Domain.Marker;
using Infrastructure.Marker;
using Persistence.Marker;
using WebMvc.Marker;

namespace ArchitectureTests;

/// <summary>
/// Base class for web architecture tests.
/// </summary>
public abstract class BaseTest
{
    protected static readonly Assembly DomainAssembly = typeof(IWebDomain).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(IWebApplication).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(IWebInfrastructure).Assembly;
    protected static readonly Assembly PersistenceAssembly = typeof(IWebPersistence).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(IWebMvc).Assembly;
}
