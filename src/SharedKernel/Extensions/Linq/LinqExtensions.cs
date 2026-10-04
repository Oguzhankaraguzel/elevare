using System.Linq.Expressions;

namespace SharedKernel.Extensions.Linq;

/// <summary>
/// Provides extension methods for LINQ-related operations on collections.
/// </summary>
public static class LinqExtensions
{
    /// <summary>
    /// Applies a filter to the queryable source if the specified condition is true.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source.</typeparam>
    /// <param name="source">The queryable data source.</param>
    /// <param name="condition">A boolean value indicating whether to apply the predicate.</param>
    /// <param name="predicate">The filter expression to apply if the condition is true.</param>
    /// <returns>
    /// A queryable containing elements that satisfy the predicate if the condition is true;
    /// otherwise, the original unfiltered queryable.
    /// </returns>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> source,
        bool condition,
        Expression<Func<T, bool>> predicate) => condition ? source.Where(predicate) : source;

    /// <summary>
    /// Returns a page of elements from the queryable source based on the specified page number and page size.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source.</typeparam>
    /// <param name="source">The queryable data source.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of elements per page.</param>
    /// <returns>A queryable containing only the elements for the requested page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="page"/> is less than 1 or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    public static IQueryable<T> Paginate<T>(
        this IQueryable<T> source,
        int page,
        int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        int start = (page - 1) * pageSize;
        return source.Skip(start).Take(pageSize);
    }
}
