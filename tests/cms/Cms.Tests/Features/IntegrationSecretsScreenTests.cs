using Application.Abstraction.Services;
using Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;
using Cms.Tests.Support;
using Domain.Entities.IntegrationSecrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Persistence.Data;
using Shouldly;
using Wasm.Configuration;

namespace Cms.Tests.Features;

/// <summary>
/// The Secrets screen shows where each value comes from and never the value itself —
/// the case it exists for being a site whose mail settings live in .env, where every
/// field used to look empty while mail worked.
/// </summary>
public sealed class IntegrationSecretsScreenTests : IDisposable
{
    private readonly IdentityTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private sealed class FakeServerConfig(Dictionary<string, string> values) : IConfigurationInspector
    {
        public string? GetServerValue(string key) => values.GetValueOrDefault(key);
        public string? GetEffectiveValue(string key) => values.GetValueOrDefault(key);
    }

    private async Task<List<IntegrationSecretResponse>> ListAsync(Dictionary<string, string> server, params (string Key, string? Value, bool IsSecret, string DataType)[] rows)
    {
        using IServiceScope scope = _host.Scope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach ((string key, string? value, bool isSecret, string dataType) in rows)
        {
            db.IntegrationSecrets.Add(new IntegrationSecret
            {
                Key = key, Value = value, DisplayName = key, IsSecret = isSecret, DataType = dataType,
                Category = IntegrationSecretCategory.Email, CreateUserId = _host.CurrentUser.UserId,
            });
        }
        await db.SaveChangesAsync(CancellationToken.None);

        return (await new GetIntegrationSecretsQueryHandler(db, new FakeServerConfig(server))
            .Handle(new GetIntegrationSecretsQuery(), CancellationToken.None)).Value;
    }

    [Fact]
    public async Task No_typed_value_leaves_the_server_secret_or_not()
    {
        List<IntegrationSecretResponse> list = await ListAsync([],
            ("Email:Host", "smtp.example.com", false, "string"),
            ("Email:Password", "encrypted", true, "string"));

        list.ShouldAllBe(s => s.Value == null);
        list.ShouldAllBe(s => s.Source == IntegrationSecretSource.Saved);
    }

    [Fact]
    public async Task A_value_from_the_server_configuration_is_reported_as_such()
    {
        List<IntegrationSecretResponse> list = await ListAsync(
            new() { ["Email:Host"] = "smtp.vectopos.example" },
            ("Email:Host", null, false, "string"),
            ("Email:UserName", null, false, "string"));

        IntegrationSecretResponse host = list.Single(s => s.Key == "Email:Host");
        host.Source.ShouldBe(IntegrationSecretSource.Server);
        host.Value.ShouldBeNull();
        list.Single(s => s.Key == "Email:UserName").Source.ShouldBe(IntegrationSecretSource.None);
    }

    [Fact]
    public async Task A_choice_shows_what_is_in_effect()
    {
        List<IntegrationSecretResponse> list = await ListAsync(
            new() { ["Email:EnableSsl"] = "false" },
            ("Email:EnableSsl", null, false, "bool"),
            ("ObjectStorage:Provider", null, false, "storage-provider"));

        list.Single(s => s.Key == "Email:EnableSsl").Value.ShouldBe("false");      // from the server
        list.Single(s => s.Key == "ObjectStorage:Provider").Value.ShouldBe("Local"); // built-in default
    }

    [Fact]
    public void The_server_value_ignores_the_secrets_layer_and_respects_precedence()
    {
        var secrets = new IntegrationSecretsConfigurationProvider(
            new Dictionary<string, string?> { ["Email:Host"] = "from-screen" }, "Host=unused", null, NullLoggerFactory.Instance);

        using ConfigurationManager configuration = new();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Host"] = "from-appsettings",
            ["Email:UserName"] = "from-appsettings",
        });
        // docker-compose passes an unset SMTP_* variable through as an empty string,
        // which overrides appsettings.json — so the server has no value for it.
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Email:UserName"] = "" });
        ((IConfigurationBuilder)configuration).Sources.Add(new IntegrationSecretsConfigurationSource(secrets));

        var inspector = new ConfigurationInspector(configuration);

        inspector.GetServerValue("Email:Host").ShouldBe("from-appsettings");
        inspector.GetEffectiveValue("Email:Host").ShouldBe("from-screen");
        inspector.GetServerValue("Email:UserName").ShouldBeNull();
        inspector.GetServerValue("Email:Port").ShouldBeNull();
    }
}
