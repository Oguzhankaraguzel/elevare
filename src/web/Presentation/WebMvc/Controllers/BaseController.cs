using Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace WebMvc.Controllers;

/// <summary>
/// Base controller providing shared helper methods for all Web controllers.
/// </summary>
public abstract class BaseController : Controller
{
    /// <summary>
    /// Throws <see cref="NotFoundException"/> which is caught by the global exception handler
    /// and rendered as a 404 page.
    /// </summary>
    protected static NotFoundException NotFoundResource(string name, object key)
        => new(name, key);

    /// <summary>
    /// Throws <see cref="NotFoundException"/> with a custom message.
    /// </summary>
    protected static NotFoundException NotFoundResource(string message)
        => new(message);
}
