using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Auth.Login;

/// <param name="Identifier">E-mail address or user name — either is accepted.</param>
public sealed record LoginCommand(string Identifier, string Password)
    : ICommand<LoginResponse>;
