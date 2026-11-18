namespace Asteria.Content.Registries;

/// <summary>
/// Read-only set of holders, optionally named by a registry tag.
/// </summary>
public class HolderSet<T> : IReadOnlyList<Holder<T>> where T : class
{
    private readonly Holder<T>[] _holders;

    public static HolderSet<T> Empty { get; } = new(Array.Empty<Holder<T>>());

    public int Count => _holders.Length;
    public Holder<T> this[int index] => _holders[index];

    public HolderSet(IEnumerable<Holder<T>> holders)
    {
        ArgumentNullException.ThrowIfNull(holders);
        _holders = holders.Distinct().ToArray();
    }

    public bool Contains(Holder<T> holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return _holders.Contains(holder);
    }

    public IEnumerator<Holder<T>> GetEnumerator() =>
        ((IEnumerable<Holder<T>>)_holders).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        _holders.GetEnumerator();

    public sealed class Named : HolderSet<T>
    {
        public TagKey<T> Key { get; }

        public Named(TagKey<T> key, IEnumerable<Holder<T>> holders)
            : base(holders)
        {
            Key = key;
        }

        public override string ToString() => $"NamedSet{{{Key}, size={Count}}}";
    }
}
