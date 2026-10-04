using System.Reflection;
using MediatR;
using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Exceptions;

namespace Application.Abstraction.Behaviors;

internal sealed class SaveChangesPipelineBehavior<TRequest, TResponse>(ICmsApplicationDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response = await next();

        if (request is IBaseCommand)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                var error = ex.ToError("Database.SaveError", "Saving the changes failed.");

                // Wrap the db exception into the appropriate Result type instead of leaking the raw exception
                if (typeof(TResponse) == typeof(Result))
                    return (TResponse)(object)Result.Failure(error);

                if (typeof(TResponse).IsGenericType &&
                    typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    Type resultType = typeof(TResponse).GetGenericArguments()[0];
                    MethodInfo? failureMethod = typeof(Result)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                        ?.MakeGenericMethod(resultType);

                    if (failureMethod is not null)
                        return (TResponse)failureMethod.Invoke(null, [error])!;
                }

                throw;
            }
        }

        return response;
    }
}
