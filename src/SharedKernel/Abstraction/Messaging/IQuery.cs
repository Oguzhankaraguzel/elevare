using MediatR;
using SharedKernel.Concrete;

namespace SharedKernel.Abstraction.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
