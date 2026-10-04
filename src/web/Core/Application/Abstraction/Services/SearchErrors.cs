using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="ISearchProvider"/>.</summary>
public static class SearchErrors
{
    /// <summary>
    /// The search could not be executed. Distinct from "no results": an empty list is
    /// a valid answer, whereas this means the query never ran and the caller should
    /// not present emptiness as fact.
    /// </summary>
    public static Error Failed(string detail) =>
        Error.Problem("Search.Failed", $"The search could not be completed: {detail}");
}
