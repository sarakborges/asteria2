namespace Asteria.Content.Registries;

/// <summary>
/// Read-only lookup surface for registry elements and named tags.
/// </summary>
public interface HolderLookup<T> : HolderGetter<T> where T : class
{
    IEnumerable<Holder<T>.Reference> ListElements();
    IEnumerable<HolderSet<T>.Named> ListTags();

    IEnumerable<RegistryKey<T>> ListElementIds() =>
        ListElements().Select(holder => holder.Key);

    IEnumerable<TagKey<T>> ListTagIds() =>
        ListTags().Select(tag => tag.Key);

    /// <summary>A read-only lookup tied to one registry and its lifecycle metadata.</summary>
    public interface RegistryLookup : HolderLookup<T>, IRegistryLookup
    {
        RegistryKey<Registry<T>> Key { get; }

        RegistryLookup FilterElements(Predicate<T> filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            return new FilteredHolderLookup<T>(this, filter);
        }
    }
}

/// <summary>
/// Filtered registry lookup. Element enumeration and direct key lookup are filtered;
/// named tags continue to come from the parent lookup, matching vanilla Delegate behavior.
/// </summary>
internal sealed class FilteredHolderLookup<T> : HolderLookup<T>.RegistryLookup where T : class
{
    private readonly HolderLookup<T>.RegistryLookup _parent;
    private readonly Predicate<T> _filter;

    public FilteredHolderLookup(
        HolderLookup<T>.RegistryLookup parent,
        Predicate<T> filter)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public RegistryKey<Registry<T>> Key => _parent.Key;
    public Lifecycle RegistryLifecycle => _parent.RegistryLifecycle;

    RegistryKey<IRegistry> IRegistryLookup.RegistryKey =>
        ((IRegistryLookup)_parent).RegistryKey;

    public Holder<T>.Reference? Get(RegistryKey<T> key)
    {
        Holder<T>.Reference? holder = _parent.Get(key);
        return holder is not null && _filter(holder.Value) ? holder : null;
    }

    public HolderSet<T>.Named? Get(TagKey<T> tag) => _parent.Get(tag);

    public IEnumerable<Holder<T>.Reference> ListElements() =>
        _parent.ListElements().Where(holder => _filter(holder.Value));

    public IEnumerable<HolderSet<T>.Named> ListTags() => _parent.ListTags();

    public bool CanSerializeIn(HolderOwner<T> owner) =>
        _parent.CanSerializeIn(owner);
}
