namespace Infrastructure.Caching;

/// <summary>
/// Bound from <c>appsettings.json → Redis</c>. An empty <see cref="ConnectionString"/>
/// (the default) means "use the in-process memory cache instead" — see
/// InfrastructureServiceRegistration.cs for the switch.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = "";
}
