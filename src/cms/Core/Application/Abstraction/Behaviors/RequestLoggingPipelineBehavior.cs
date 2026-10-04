using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Abstraction.Behaviors;

internal class RequestLoggingPipelineBehavior<TRequest, TResponse>(ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling request: {RequestName}", requestName);

        TResponse result = await next();

        if (result.IsSuccess)
            logger.LogInformation("Request {RequestName} handled successfully", requestName);
        
        else
            logger.LogError("Request {RequestName} failed with error: {Error}", requestName, result.Error);
        
        return result;
    }
}
