using Application.Abstraction.Services;
using Infrastructure.Captcha;
using Infrastructure.Caching;
using Infrastructure.Logging;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddWebInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CaptchaOptions>(configuration.GetSection(CaptchaOptions.SectionName));
        services.AddHttpClient<ICaptchaVerifier, CaptchaVerifier>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHttpClient<IErrorWebhookForwarder, ErrorWebhookForwarder>();
        services.AddScoped<IPreviewTokenValidator, PreviewTokenValidator>();

        // ── Cache — Redis when configured, in-process memory otherwise. Which
        // backend is active is a deployment-time choice (an empty connection
        // string means "no Redis available"), not something flippable live.
        // The Redis connection is deliberately NOT a plain
        // ConnectionMultiplexer.Connect(...) factory: that throws on first use
        // when the server is unreachable, which turns a cache outage into an
        // HTTP 500 on every request. See RedisConnection/IRedisConnection.
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        string? redisConnectionString = configuration.GetSection(RedisOptions.SectionName)[nameof(RedisOptions.ConnectionString)];
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddSingleton<IRedisConnection, RedisConnection>();
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }
}
