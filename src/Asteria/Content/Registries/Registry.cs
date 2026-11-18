using System.Collections;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// MappedRegistry-style lookup maps for keys, values, IDs, holders, and tags.
/// Registration is bootstrap-only; tag contents may be rebound after freezing.
/// </summary>
public class Registry<T> :
    IReadOnlyCollection<RegistryEntry<T>>,
    IIdMap<T>,
    HolderOwner<T>
    where T : class
{
    private readonly Dictionary<ResourceLocation, RegistryEntry<T>> _entriesByLocation = new();
    private readonly Dictionary<RegistryKey<T>, RegistryEntry<T>> _entriesByKey = new();
    private readonly Dictionary<T, RegistryEntry<T>> _entriesByValue =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<RegistryEntry<T>> _entriesById = new();
    private readonly IReadOnlyList<RegistryEntry<T>> _readOnlyEntries;
    private readonly Dictionary<T, Holder<T>.Reference> _unregisteredIntrusiveHolders =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TagKey<T>, HolderSet<T>.Named> _tags = new();
    private readonly bool _allowIntrusiveHolders;

    public ResourceLocation Location { get; }
    public int Count => _entriesById.Count;
    public bool IsFrozen { get; protected set; }
    public IReadOnlyList<RegistryEntry<T>> Entries => _readOnlyEntries;

    public Registry(ResourceLocation location, bool allowIntrusiveHolders = false)
    {
        if (!location.IsValid)
        {
            throw new ArgumentException("A valid registry location is required.", nameof(location));
        }

        Location = location;
        _allowIntrusiveHolders = allowIntrusiveHolders;
        _readOnlyEntries = _entriesById.AsReadOnly();
    }

    public RegistryEntry<T> Register(ResourceLocation location, T value)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);

        if (!location.IsValid)
        {
            throw new ArgumentException("A valid entry location is required.", nameof(location));
        }

        if (_entriesByLocation.ContainsKey(location))
        {
            throw new InvalidOperationException(
                $"Registry '{Location}' already contains an entry named '{location}'.");
        }

        if (_entriesByValue.ContainsKey(value))
        {
            throw new InvalidOperationException(
                $"The same object instance cannot be registered twice in '{Location}'.");
        }

        var key = new RegistryKey<T>(Location, location);
        Holder<T>.Reference holder;

        if (_unregisteredIntrusiveHolders.Remove(value, out Holder<T>.Reference? intrusiveHolder))
        {
            holder = intrusiveHolder;
            holder.BindKey(key);
        }
        else
        {
            holder = Holder<T>.Reference.CreateStandAlone(this, key);
            holder.BindValue(value);
        }

        var entry = new RegistryEntry<T>(holder, _entriesById.Count);
        _entriesByLocation.Add(location, entry);
        _entriesByKey.Add(key, entry);
        _entriesByValue.Add(value, entry);
        _entriesById.Add(entry);

        return entry;
    }

    public Holder<T>.Reference CreateIntrusiveHolder(T value)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);

        if (!_allowIntrusiveHolders)
        {
            throw new InvalidOperationException($"Registry '{Location}' does not allow intrusive holders.");
        }

        if (_entriesByValue.ContainsKey(value) || _unregisteredIntrusiveHolders.ContainsKey(value))
        {
            throw new InvalidOperationException("An intrusive holder already exists for this value.");
        }

        Holder<T>.Reference holder = Holder<T>.Reference.CreateIntrusive(this, value);
        _unregisteredIntrusiveHolders.Add(value, holder);
        return holder;
    }

    public virtual T Get(ResourceLocation location) => GetEntry(location).Value;

    public virtual RegistryEntry<T> GetEntry(ResourceLocation location)
    {
        if (_entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException(
            $"Registry '{Location}' does not contain an entry named '{location}'.");
    }

    public RegistryEntry<T> GetEntry(RegistryKey<T> key)
    {
        if (key.RegistryLocation != Location || !_entriesByKey.TryGetValue(key, out RegistryEntry<T>? entry))
        {
            throw new KeyNotFoundException($"Registry '{Location}' does not contain key '{key}'.");
        }

        return entry;
    }

    public bool TryGetEntry(ResourceLocation location, out RegistryEntry<T>? entry) =>
        _entriesByLocation.TryGetValue(location, out entry);

    public bool TryGetEntry(RegistryKey<T> key, out RegistryEntry<T>? entry) =>
        key.RegistryLocation == Location && _entriesByKey.TryGetValue(key, out entry);

    public bool TryGetValue(ResourceLocation location, out T? value)
    {
        if (_entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public Holder<T>.Reference? GetHolder(ResourceLocation location) =>
        _entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry)
            ? entry.Holder
            : null;

    public Holder<T>.Reference? GetHolder(RegistryKey<T> key) =>
        _entriesByKey.TryGetValue(key, out RegistryEntry<T>? entry)
            ? entry.Holder
            : null;

    public Holder<T>.Reference GetHolderOrThrow(RegistryKey<T> key) =>
        GetHolder(key) ?? throw new KeyNotFoundException($"Registry '{Location}' does not contain key '{key}'.");

    public Holder<T>.Reference? GetHolderById(int id) =>
        (uint)id < (uint)_entriesById.Count ? _entriesById[id].Holder : null;

    public Holder<T> WrapAsHolder(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry)
            ? entry.Holder
            : Holder<T>.Direct(value);
    }

    public virtual int GetId(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry) ? entry.Id : -1;
    }

    public T GetById(int id) => ById(id).Value;

    public virtual ResourceLocation? GetKey(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry)
            ? entry.Key.Location
            : null;
    }

    public virtual RegistryEntry<T> ById(int id)
    {
        if ((uint)id >= (uint)_entriesById.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "The runtime registry ID does not exist.");
        }

        return _entriesById[id];
    }

    public void BindTags(IEnumerable<KeyValuePair<TagKey<T>, IEnumerable<ResourceLocation>>> tagDefinitions)
    {
        ArgumentNullException.ThrowIfNull(tagDefinitions);

        var holderTags = new Dictionary<Holder<T>.Reference, List<TagKey<T>>>();
        foreach (RegistryEntry<T> entry in _entriesById)
        {
            holderTags.Add(entry.Holder, new List<TagKey<T>>());
        }

        var reboundTags = new Dictionary<TagKey<T>, HolderSet<T>.Named>();
        foreach (KeyValuePair<TagKey<T>, IEnumerable<ResourceLocation>> definition in tagDefinitions)
        {
            TagKey<T> tag = definition.Key;
            if (tag.RegistryLocation != Location)
            {
                throw new ArgumentException(
                    $"Tag '{tag}' belongs to another registry, not '{Location}'.",
                    nameof(tagDefinitions));
            }

            var members = new List<Holder<T>>();
            foreach (ResourceLocation location in definition.Value)
            {
                if (!_entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry))
                {
                    throw new KeyNotFoundException(
                        $"Tag '{tag}' references missing entry '{location}' in registry '{Location}'.");
                }

                members.Add(entry.Holder);
                holderTags[entry.Holder].Add(tag);
            }

            reboundTags.Add(tag, new HolderSet<T>.Named(tag, members));
        }

        foreach (RegistryEntry<T> entry in _entriesById)
        {
            entry.Holder.BindTags(holderTags[entry.Holder]);
        }

        _tags.Clear();
        foreach ((TagKey<T> tag, HolderSet<T>.Named holders) in reboundTags)
        {
            _tags.Add(tag, holders);
        }
    }

    public HolderSet<T>.Named? GetTag(TagKey<T> tag) =>
        _tags.TryGetValue(tag, out HolderSet<T>.Named? holders) ? holders : null;

    public HolderSet<T> GetTagOrEmpty(TagKey<T> tag) =>
        GetTag(tag) ?? HolderSet<T>.Empty;

    public IEnumerable<HolderSet<T>.Named> ListTags() => _tags.Values;

    public virtual void Freeze()
    {
        if (_unregisteredIntrusiveHolders.Count != 0)
        {
            throw new InvalidOperationException(
                $"Registry '{Location}' cannot freeze with {_unregisteredIntrusiveHolders.Count} unregistered intrusive holder(s).");
        }

        if (_tags.Count == 0)
        {
            BindTags(Array.Empty<KeyValuePair<TagKey<T>, IEnumerable<ResourceLocation>>>());
        }

        IsFrozen = true;
    }

    public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(this, owner);

    public IEnumerator<RegistryEntry<T>> GetEnumerator() => _entriesById.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected void EnsureWritable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException(
                $"Registry '{Location}' is frozen; no more entries can be registered.");
        }
    }
}
