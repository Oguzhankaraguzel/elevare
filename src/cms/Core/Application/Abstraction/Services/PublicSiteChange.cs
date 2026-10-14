using SharedKernel.Content;

namespace Application.Abstraction.Services;

/// <summary>
/// What a set of saved changes means for the public site's cached pages: nothing,
/// the pages carrying some <see cref="PageCacheTags"/>, or everything.
/// </summary>
/// <remarks>
/// Built by the database context from the entries it saves (see
/// <c>PublicSiteChangeClassifier</c>), so it follows what a command actually wrote,
/// not what the command was meant to do. Anything it cannot place is
/// <see cref="IsEverything"/>.
/// </remarks>
public sealed class PublicSiteChange
{
    private readonly HashSet<string> _tags = new(StringComparer.Ordinal);

    /// <summary>A change the site cannot narrow down: every cached page goes.</summary>
    public static PublicSiteChange Everything() => new() { IsEverything = true };

    public static PublicSiteChange ForTags(params string[] tags)
    {
        PublicSiteChange change = new();
        foreach (string tag in tags)
            change.AddTag(tag);
        return change;
    }

    public bool IsEverything { get; private set; }

    /// <summary>
    /// How many saves that wrote something contributed. None after a successful
    /// command means it changed nothing this object could see — or changed it some
    /// other way, which is why that case clears everything.
    /// </summary>
    public int Saves { get; private set; }

    public IReadOnlyCollection<string> Tags => _tags;

    /// <summary>True when nothing a visitor sees was touched.</summary>
    public bool IsEmpty => !IsEverything && _tags.Count == 0;

    public void AddTag(string tag)
    {
        if (!IsEverything)
            _tags.Add(tag);
    }

    public void MarkEverything()
    {
        IsEverything = true;
        _tags.Clear();
    }

    public void CountSave() => Saves++;

    /// <summary>Folds <paramref name="other"/> in, for requests sent together.</summary>
    public void Merge(PublicSiteChange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Saves += other.Saves;
        if (IsEverything)
            return;
        if (other.IsEverything)
        {
            MarkEverything();
            return;
        }
        _tags.UnionWith(other._tags);
    }
}
