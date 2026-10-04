using Application.Abstraction.Behaviors;
using Application.Marker;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // One gate (one semaphore) per DI scope — i.e. one per Blazor Server circuit —
        // so DbConcurrencyGuardPipelineBehavior below can serialize every request that
        // scope sends against the shared scoped ICmsApplicationDbContext.
        services.AddScoped<DbContextConcurrencyGate>();

        // MediatR – scan the Application assembly for all IRequestHandler<,> implementations.
        // Behaviors are applied in registration order (outermost first):
        //   1. DbConcurrencyGuardPipelineBehavior – serializes DbContext access across the circuit
        //   2. RequestLoggingPipelineBehavior     – logs every request/response
        //   3. PermissionPipelineBehavior         – refuses requests the caller is not permitted to make
        //   4. ValidationPipelineBehavior         – runs FluentValidation before the handler
        //   5. PublicSiteCacheInvalidationPipelineBehavior – tells the public site to drop
        //      its rendered pages; outside SaveChanges so it sees only committed changes
        //   6. SaveChangesPipelineBehavior        – commits a command's changes
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ICmsApplication).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(DbConcurrencyGuardPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequestLoggingPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PermissionPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PublicSiteCacheInvalidationPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(SaveChangesPipelineBehavior<,>));
        });

        // FluentValidation – discover all IValidator<T> implementations in the Application assembly.
        services.AddValidatorsFromAssembly(typeof(ICmsApplication).Assembly, includeInternalTypes: true);

        return services;
    }
}
