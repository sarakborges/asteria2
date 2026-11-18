namespace Asteria.Content.Registries;

/// <summary>A direct or registry-named set of holders.</summary>
public class HolderSet<T> : IReadOnlyList<Holder<T>> where T : class
{
    private readonly IReadOnlyList<Holder<T>>? _contents;

    public static HolderSet<T> Empty { get; } = new(Array.Empty<Holder<T>>());
    public virtual bool IsBound => _contents is not null;
    public virtual int Count => Contents.Count;
    public virtual Holder<T> this[int index] => Contents[index];
    public virtual IReadOnlyList<Holder<T>> Contents =>
        _contents ?? throw new InvalidOperationException("Trying to access an unbound holder set.");

    public HolderSet(IEnumerable<Holder<T>> holders)
    {
        ArgumentNullException.ThrowIfNull(holders);
        _contents = Array.AsReadOnly(holders.Distinct().ToArray());
    }

    protected HolderSet()
    {
    }

    public virtual bool Contains(Holder<T> holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return Contents.Contains(holder);
    }

    public virtual bool CanSerializeIn(HolderOwner<T> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return Contents.All(holder => holder.CanSerializeIn(owner));
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
            foreach (Holder<T> holder in values)
            {
                if (!holder.CanSerializeIn(_owner))
                    throw new InvalidOperationException($"Can't create named set {Key} containing value {holder} from outside its registry.");
                if (holder.Kind != HolderKind.Reference)
                    throw new InvalidOperationException($"Found direct holder value in tag {Key}.");
            }
            _boundContents = Array.AsReadOnly(values.Distinct().ToArray());
        }

        public override bool CanSerializeIn(HolderOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return _owner.CanSerializeIn(owner);
        }

        public override string ToString() => $"NamedSet({Key}, size={Contents.Count})";
    }
}
