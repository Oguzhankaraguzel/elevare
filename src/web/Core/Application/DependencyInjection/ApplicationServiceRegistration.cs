using Application.Abstraction.Services;
using Application.Marker;
using Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(IWebApplication).Assembly));

        services.AddScoped<TemplateResolutionService>();
        services.AddScoped<LanguageSwitcherResolutionService>();
        services.AddScoped<PageListingResolutionService>();
        services.AddScoped<AdjacentPageResolutionService>();
        services.AddScoped<BreadcrumbResolutionService>();
        services.AddScoped<ResponsiveImageResolutionService>();
        services.AddScoped<SystemPageProvider>();

        services.AddSingleton<ILanguageDirectory, LanguageDirectory>();
        services.AddSingleton<IMaintenanceState, MaintenanceState>();
        services.AddSingleton<IWwwRedirectState, WwwRedirectState>();
        services.AddHostedService<SiteStateRefreshHostedService>();

        services.AddScoped<ISearchProvider, SqlSearchProvider>();

        return services;
    }
}
