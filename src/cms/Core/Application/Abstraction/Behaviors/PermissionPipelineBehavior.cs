using System.Reflection;
using Application.Abstraction.Security;
using Application.Abstraction.Services.Authentication;
using MediatR;
using SharedKernel.Concrete;

namespace Application.Abstraction.Behaviors;

/// <summary>
/// Refuses a request whose declared permission the caller does not hold.
/// <para>
/// This is the only place authorisation is actually enforced for application
/// requests. It runs before the handler, so every entry point — a Blazor
/// component, a controller, a background job replaying a command — is covered by
/// the same check, and adding a new caller cannot forget it.
/// </para>
/// </summary>
internal sealed class PermissionPipelineBehavior<TRequest, TResponse>(IUserContext userContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    /// <summary>
    /// Resolved once per closed generic rather than per call: the reflection is the
    /// expensive part, and the answer cannot change for a given request type.
    /// </summary>
    private static readonly string? Required = ReadRequiredPermission();

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (Required is null || userContext.HasPermission(Required))
            return await next();

        return Denied();
    }

    private static string? ReadRequiredPermission()
    {
        if (!typeof(IRequirePermission).IsAssignableFrom(typeof(TRequest)))
            return null;

        // Static abstract members are reached through the interface map rather than
        // the type's own members, which is where the compiler puts the implementation.
        PropertyInfo? property = typeof(TRequest).GetProperty(
            nameof(IRequirePermission.RequiredPermission),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        return property?.GetValue(null) as string;
    }

    /// <summary>
    /// Shaped as a failed <see cref="Result"/> so callers report it the way they
    /// report every other refusal. Throwing here would surface as a 500 and read
    /// like a crash rather than a decision.
    /// </summary>
    private static TResponse Denied()
    {
        Error error = PermissionErrors.NotGranted(Required!);

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        if (typeof(TResponse).IsGenericType
            && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            Type valueType = typeof(TResponse).GetGenericArguments()[0];
            MethodInfo? failure = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])
                ?.MakeGenericMethod(valueType);

            if (failure is not null)
                return (TResponse)failure.Invoke(null, [error])!;
        }

        // A request that declares a permission but does not return a Result has no
        // way to say "refused" — failing loudly beats running it unauthorised.
        throw new UnauthorizedAccessException(error.Description);
    }
}
