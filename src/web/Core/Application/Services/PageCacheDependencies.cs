using SharedKernel.Content;

namespace Application.Services;

/// <summary>
/// What the page being rendered was built from, collected by the resolvers as they
/// read it and put on the cached response as <see cref="PageCacheTags"/> — so the
/// CMS can evict exactly the pages a change touches. One per request.
/// </summary>
public sealed class PageCacheDependencies
{
    private readonly HashSet<string> _tags = new(StringComparer.Ordinal) { PageCacheTags.All };

    public IReadOnlyCollection<string> Tags => _tags;

    public void Add(string tag) => _tags.Add(tag);
}
