namespace Asteria.Content.Registries;

/// <summary>Prepared replacement of a registry's tag snapshot.</summary>
public sealed class PendingTagReload<T> where T : class
{
    private readonly Action _apply;

    public RegistryKey<Registry<T>> Key { get; }
    public HolderLookup<T>.RegistryLookup Lookup { get; }
    public int Size { get; }

    internal PendingTagReload(
        RegistryKey<Registry<T>> key,
        HolderLookup<T>.RegistryLookup lookup,
        int size,
        Action apply)
    {
        Key = key;
        Lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        Size = size;
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public void Apply() => _apply();
}

internal sealed class PendingTagReloadLookup<T> : HolderLookup<T>.RegistryLookup where T : class
{
    private readonly Registry<T> _registry;
    private readonly IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> _tags;

    public PendingTagReloadLookup(
        Registry<T> registry,
        IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> tags)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    public RegistryKey<Registry<T>> Key => _registry.Key;
    public Lifecycle RegistryLifecycle => _registry.RegistryLifecycle;

    public Holder<T>.Reference? Get(RegistryKey<T> key) =>
        ((HolderGetter<T>)_registry).Get(key);

    public HolderSet<T>.Named? Get(TagKey<T> tag) =>
        _tags.TryGetValue(tag, out HolderSet<T>.Named? holders) ? holders : null;

    public IEnumerable<Holder<T>.Reference> ListElements() =>
        _registry.ListElements();

    public IEnumerable<HolderSet<T>.Named> ListTags() =>
        _tags.Values;

    public bool CanSerializeIn(HolderOwner<T> owner) =>
        _registry.CanSerializeIn(owner);
}
