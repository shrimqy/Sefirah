namespace Sefirah.Data.Models.Messages;

/// <summary>The phone's message ids for one thread, newest first.</summary>
public sealed class ThreadMessageIndex
{
    private readonly List<long> ids = [];
    private readonly HashSet<long> lookup = [];

    public ThreadMessageIndex(IEnumerable<long> newestFirst)
    {
        foreach (var id in newestFirst)
        {
            if (lookup.Add(id))
                ids.Add(id);
        }
    }

    public bool Contains(long id) => lookup.Contains(id);

    public bool IsSubsetOf(IEnumerable<long> other) => lookup.IsSubsetOf(other);

    public void AddNewest(long id)
    {
        if (lookup.Add(id))
            ids.Insert(0, id);
    }

    public void Remove(long id)
    {
        if (lookup.Remove(id))
            ids.Remove(id);
    }

    /// <summary>Newest <paramref name="count"/> ids not in <paramref name="shown"/>, so holes come before older pages.</summary>
    public List<long> TakeNotShown(IReadOnlySet<long> shown, int count) =>
        [.. ids.Where(id => !shown.Contains(id)).Take(count)];

    public ThreadMessageIndex Clone() => new(ids);
}
