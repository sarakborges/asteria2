namespace Asteria.Content.Registries;

/// <summary>
/// Represents the two vanilla HolderSet.unwrap() alternatives:
/// a tag key or a direct list of holders.
/// </summary>
public readonly struct HolderSetUnwrap<T> where T : class
{
    public bool IsTag { get; }
    public TagKey<T>? Tag { get; }
    public IReadOnlyList<Holder<T>>? Values { get; }

    private HolderSetUnwrap(TagKey<T>? tag, IReadOnlyList<Holder<T>>? values)
    {
        IsTag = tag.HasValue;
        Tag = tag;
        Values = values;
    }

    public static HolderSetUnwrap<T> FromTag(TagKey<T> tag) =>
        new(tag, null);

    public static HolderSetUnwrap<T> FromValues(IReadOnlyList<Holder<T>> values) =>
        new(null, values ?? throw new ArgumentNullException(nameof(values)));
}

/// <summary>A direct or registry-named set of holders.</summary>
public class HolderSet<T> : IReadOnlyList<Holder<T>> where T : class
{
    private readonly IReadOnlyList<Holder<T>>? _contents;
    private HashSet<Holder<T>>? _contentsSet;

    public static HolderSet<T> Empty { get; } = new(Array.Empty<Holder<T>>());
    public virtual bool IsBound => _contents is not null;
    public virtual int Count => Contents.Count;
    public virtual Holder<T> this[int index] => Contents[index];
    public virtual IReadOnlyList<Holder<T>> Contents =>
        _contents ?? throw new InvalidOperationException("Trying to access an unbound holder set.");

    public HolderSet(IEnumerable<Holder<T>> holders)
    {
        ArgumentNullException.ThrowIfNull(holders);
        Holder<T>[] values = holders.ToArray();
        if (values.Any(holder => holder is null))
        {
            throw new ArgumentException("Holder sets cannot contain null values.", nameof(holders));
        }

        _contents = Array.AsReadOnly(values);
    }

    protected HolderSet()
    {
    }

    public virtual Holder<T> Get(int index) => Contents[index];

    public virtual bool Contains(Holder<T> holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        _contentsSet ??= new HashSet<Holder<T>>(Contents);
        return _contentsSet.Contains(holder);
    }

    public virtual Holder<T>? GetRandomElement(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        IReadOnlyList<Holder<T>> contents = Contents;
        return contents.Count == 0 ? null : contents[random.Next(contents.Count)];
    }

    public virtual HolderSetUnwrap<T> Unwrap() =>
        HolderSetUnwrap<T>.FromValues(Contents);

    public virtual TagKey<T>? UnwrapKey() => null;

    public virtual IEnumerable<Holder<T>> Stream() => Contents;

    public virtual bool CanSerializeIn(HolderOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return true;
    }

    public override bool Equals(object? obj)
    {
        if (this is Named || obj is Named)
        {
            return ReferenceEquals(this, obj);
        }

        return ReferenceEquals(this, obj) ||
            obj is HolderSet<T> other && Contents.SequenceEqual(other.Contents);
    }

    public override int GetHashCode()
    {
        if (this is Named)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }

        var hash = new HashCode();
        foreach (Holder<T> holder in Contents)
        {
            hash.Add(holder);
        }

        return hash.ToHashCode();
    }

    public virtual IEnumerator<Holder<T>> GetEnumerator() => Contents.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public sealed class Named : HolderSet<T>
    {
        private readonly HolderOwner<T> _owner;
        private IReadOnlyList<Holder<T>>? _boundContents;

        public TagKey<T> Key { get; }
        public override bool IsBound => _boundContents is not null;
        public override int Count => Contents.Count;
        public override Holder<T> this[int index] => Contents[index];
        public override IReadOnlyList<Holder<T>> Contents =>
            _boundContents ?? throw new InvalidOperationException($"Trying to access unbound tag '{Key}' from its registry.");

        internal Named(HolderOwner<T> owner, TagKey<T> key)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Key = key;
        }

        internal void Bind(IEnumerable<Holder<T>> holders)
        {
            ArgumentNullException.ThrowIfNull(holders);
            Holder<T>[] values = holders.ToArray();
            if (values.Any(holder => holder is null))
            {
                throw new ArgumentException("Holder sets cannot contain null values.", nameof(holders));
            }

            _boundContents = Array.AsReadOnly(values);
        }

        public override Holder<T> Get(int index) => Contents[index];

        public override bool Contains(Holder<T> holder)
        {
            ArgumentNullException.ThrowIfNull(holder);
            return holder.Is(Key);
        }

        public override HolderSetUnwrap<T> Unwrap() =>
            HolderSetUnwrap<T>.FromTag(Key);

        public override TagKey<T>? UnwrapKey() => Key;

        public override IEnumerable<Holder<T>> Stream() => Contents;

        public override bool CanSerializeIn(HolderOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return _owner.CanSerializeIn(owner);
        }

        public override string ToString() => $"NamedSet({Key})[{_boundContents}]";
    }
}
