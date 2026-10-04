using MediatR;
using SharedKernel.Concrete;

namespace SharedKernel.Abstraction.Messaging;

public interface ICommand : IRequest<Result>, IBaseCommand;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;
