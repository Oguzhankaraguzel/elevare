namespace Application.Exceptions;

/// <summary>
/// Thrown when a requested resource cannot be found.
/// Caught by the global exception handler to return a 404 response.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : base("The requested resource was not found.") { }

    public NotFoundException(string message)
        : base(message) { }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException) { }

    public NotFoundException(string name, object key)
        : base($"'{name}' with key '{key}' was not found.") { }
}
