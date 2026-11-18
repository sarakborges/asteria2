using System.Collections;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>MappedRegistry-style maps, stable holders, intrusive registration, and reloadable tags.</summary>
public class Registry<T> : IReadOnlyCollection<RegistryEntry<T>>, IIdMap<T>, HolderOwner<T> where T : class
{
    private readonly Dictionary<ResourceLocation, RegistryEntry<T>> _entriesByLocation = new();
    private readonly Dictionary<RegistryKey<T>, RegistryEntry<T>> _entriesByKey = new();
    private readonly Dictionary<T, RegistryEntry<T>> _entriesByValue = new(ReferenceEqualityComparer.Instance);
    private readonly List<RegistryEntry<T>> _entriesById = new();
    private readonly IReadOnlyList<RegistryEntry<T>> _readOnlyEntries;
    private readonly Dictionary<RegistryKey<T>, Holder<T>.Reference> _holdersByKey = new();
    private readonly Dictionary<T, Holder<T>.Reference>? _unregisteredIntrusiveHolders;
    private Dictionary<TagKey<T>, HolderSet<T>.Named> _registrationTags = new();
    private IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named>? _boundTags;

    public ResourceLocation Location { get; }
    public int Count => _entriesById.Count;
    public bool IsFrozen { get; protected set; }
    public IReadOnlyList<RegistryEntry<T>> Entries => _readOnlyEntries;

    public Registry(ResourceLocation location, bool allowIntrusiveHolders = false)
    {
        if (!location.IsValid) throw new ArgumentException("A valid registry location is required.", nameof(location));
        Location = location;
        _unregisteredIntrusiveHolders = allowIntrusiveHolders
            ? new Dictionary<T, Holder<T>.Reference>(ReferenceEqualityComparer.Instance)
            : null;
        _readOnlyEntries = _entriesById.AsReadOnly();
    }

    public RegistryEntry<T> Register(ResourceLocation location, T value)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);
        if (!location.IsValid) throw new ArgumentException("A valid entry location is required.", nameof(location));
        if (_entriesByLocation.ContainsKey(location))
            throw new InvalidOperationException($"Registry '{Location}' already contains an entry named '{location}'.");
        if (_entriesByValue.ContainsKey(value))
            throw new InvalidOperationException($"The same object instance cannot be registered twice in '{Location}'.");

        var key = new RegistryKey<T>(Location, location);
        Holder<T>.Reference holder;
        if (_unregisteredIntrusiveHolders is not null)
        {
            if (!_unregisteredIntrusiveHolders.Remove(value, out Holder<T>.Reference? intrusiveHolder))
                throw new InvalidOperationException($"Missing intrusive holder for {key}:{value}.");
            holder = intrusiveHolder;
            holder.BindKey(key);
        }
        else if (!_holdersByKey.TryGetValue(key, out holder!))
        {
            holder = Holder<T>.Reference.CreateStandAlone(this, key);
            _holdersByKey.Add(key, holder);
        }

        var entry = new RegistryEntry<T>(holder, _entriesById.Count);
        _entriesByLocation.Add(location, entry);
        _entriesByKey.Add(key, entry);
        _entriesByValue.Add(value, entry);
        _entriesById.Add(entry);
        _holdersByKey[key] = holder;
        return entry;
    }

    public Holder<T>.Reference CreateIntrusiveHolder(T value)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);
        if (_unregisteredIntrusiveHolders is null)
            throw new InvalidOperationException($"Registry '{Location}' does not allow intrusive holders.");
        if (_entriesByValue.ContainsKey(value))
            throw new InvalidOperationException("The value is already registered in this registry.");
        if (_unregisteredIntrusiveHolders.TryGetValue(value, out Holder<T>.Reference? existing))
            return existing;
        Holder<T>.Reference holder = Holder<T>.Reference.CreateIntrusive(this, value);
        _unregisteredIntrusiveHolders.Add(value, holder);
        return holder;
    }

    public virtual T Get(ResourceLocation location) => GetEntry(location).Value;

    public virtual RegistryEntry<T> GetEntry(ResourceLocation location)
    {
        if (_entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry)) return entry;
        throw new KeyNotFoundException($"Registry '{Location}' does not contain an entry named '{location}'.");
    }

    public RegistryEntry<T> GetEntry(RegistryKey<T> key)
    {
        if (key.RegistryLocation != Location || !_entriesByKey.TryGetValue(key, out RegistryEntry<T>? entry))
            throw new KeyNotFoundException($"Registry '{Location}' does not contain key '{key}'.");
        return entry;
    }

    public bool TryGetEntry(ResourceLocation location, out RegistryEntry<T>? entry) =>
        _entriesByLocation.TryGetValue(location, out entry);

    public bool TryGetEntry(RegistryKey<T> key, out RegistryEntry<T>? entry)
    {
        if (key.RegistryLocation == Location && _entriesByKey.TryGetValue(key, out entry)) return true;
        entry = null;
        return false;
    }

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
        _entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry) ? entry.Holder : null;

    public Holder<T>.Reference? GetHolder(RegistryKey<T> key) =>
        _entriesByKey.TryGetValue(key, out RegistryEntry<T>? entry) ? entry.Holder : null;

    public Holder<T>.Reference GetHolderOrThrow(RegistryKey<T> key) =>
        GetHolder(key) ?? throw new KeyNotFoundException($"Registry '{Location}' does not contain key '{key}'.");

    public Holder<T>.Reference? GetHolderById(int id) =>
        (uint)id < (uint)_entriesById.Count ? _entriesById[id].Holder : null;

    public Holder<T> WrapAsHolder(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry) ? entry.Holder : Holder<T>.Direct(value);
    }

    public HolderGetter<T> CreateRegistrationLookup()
    {
        EnsureWritable();
        return new RegistrationLookup(this);
    }

    private Holder<T>.Reference GetOrCreateHolderOrThrow(RegistryKey<T> key)
    {
        if (key.RegistryLocation != Location) throw new ArgumentException($"Key '{key}' belongs to another registry.", nameof(key));
        if (_entriesByKey.TryGetValue(key, out RegistryEntry<T>? registered)) return registered.Holder;
        if (_unregisteredIntrusiveHolders is not null)
            throw new InvalidOperationException("This registry can't create new holders without a value.");
        EnsureWritable();
        if (!_holdersByKey.TryGetValue(key, out Holder<T>.Reference? holder))
        {
            holder = Holder<T>.Reference.CreateStandAlone(this, key);
            _holdersByKey.Add(key, holder);
        }
        return holder;
    }

    private HolderSet<T>.Named GetOrCreateTagForRegistration(TagKey<T> tag)
    {
        if (tag.RegistryLocation != Location) throw new ArgumentException($"Tag '{tag}' belongs to another registry.", nameof(tag));
        EnsureWritable();
        if (!_registrationTags.TryGetValue(tag, out HolderSet<T>.Named? holders))
        {
            holders = new HolderSet<T>.Named(this, tag);
            _registrationTags.Add(tag, holders);
        }
        return holders;
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
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry) ? entry.Key.Location : null;
    }

    public virtual RegistryEntry<T> ById(int id)
    {
        if ((uint)id >= (uint)_entriesById.Count)
            throw new ArgumentOutOfRangeException(nameof(id), id, "The runtime registry ID does not exist.");
        return _entriesById[id];
    }

    public void BindTags(IEnumerable<KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>>> tagDefinitions)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(tagDefinitions);
        foreach (KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>> definition in tagDefinitions)
            GetOrCreateTagForRegistration(definition.Key).Bind(definition.Value);
    }

    public void BindAllTagsToEmpty()
    {
        EnsureWritable();
        foreach (HolderSet<T>.Named tag in _registrationTags.Values)
            tag.Bind(Array.Empty<Holder<T>>());
    }

    public HolderSet<T>.Named? GetTag(TagKey<T> tag)
    {
        IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> tags =
            _boundTags ?? throw new InvalidOperationException("Tags not bound.");
        return tags.TryGetValue(tag, out HolderSet<T>.Named? holders) ? holders : null;
    }

    public HolderSet<T> GetTagOrEmpty(TagKey<T> tag) => GetTag(tag) ?? HolderSet<T>.Empty;

    public IEnumerable<HolderSet<T>.Named> ListTags() =>
        (_boundTags ?? throw new InvalidOperationException("Tags not bound.")).Values;

    public PendingTagReload<T> PrepareTagReload(
        IEnumerable<KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>>> tagDefinitions)
    {
        if (!IsFrozen) throw new InvalidOperationException("Tag reload is only valid after registry freeze.");
        ArgumentNullException.ThrowIfNull(tagDefinitions);
        var pending = new Dictionary<TagKey<T>, (HolderSet<T>.Named Set, Holder<T>[] Values)>();

        foreach (KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>> definition in tagDefinitions)
        {
            TagKey<T> key = definition.Key;
            if (key.RegistryLocation != Location)
                throw new ArgumentException($"Tag '{key}' belongs to another registry.", nameof(tagDefinitions));

            HolderSet<T>.Named set = _boundTags!.TryGetValue(key, out HolderSet<T>.Named? existing)
                ? existing
                : new HolderSet<T>.Named(this, key);
            pending.Add(key, (set, definition.Value.ToArray()));
        }

        return new PendingTagReload<T>(() =>
        {
            var updated = new Dictionary<TagKey<T>, HolderSet<T>.Named>();
            foreach (var pair in pending)
            {
                pair.Value.Set.Bind(pair.Value.Values);
                updated.Add(pair.Key, pair.Value.Set);
            }
            _boundTags = updated;
            RefreshTagsInHolders(updated);
        });
    }

    public virtual void Freeze()
    {
        if (IsFrozen) return;
        IsFrozen = true;

        foreach (var pair in _entriesByValue)
            pair.Value.Holder.BindValue(pair.Key);

        RegistryKey<T>[] unboundEntries = _holdersByKey
            .Where(pair => !pair.Value.IsBound)
            .Select(pair => pair.Key)
            .ToArray();
        if (unboundEntries.Length > 0)
            throw new InvalidOperationException($"Unbound values in registry '{Location}': {string.Join(", ", unboundEntries.Select(key => key.Location))}");

        if (_unregisteredIntrusiveHolders is not null && _unregisteredIntrusiveHolders.Count != 0)
            throw new InvalidOperationException($"Some intrusive holders were not registered: {_unregisteredIntrusiveHolders.Count}");

        foreach (HolderSet<T>.Named tag in _registrationTags.Values)
            if (!tag.IsBound) throw new InvalidOperationException($"Unbound tags in registry '{Location}'.");

        var frozenTags = new Dictionary<TagKey<T>, HolderSet<T>.Named>(_registrationTags);
        _boundTags = frozenTags;
        RefreshTagsInHolders(frozenTags);
    }

    private void RefreshTagsInHolders(IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> tags)
    {
        var tagsByHolder = new Dictionary<Holder<T>.Reference, List<TagKey<T>>>(ReferenceEqualityComparer.Instance);
        foreach (RegistryEntry<T> entry in _entriesById)
            tagsByHolder.Add(entry.Holder, new List<TagKey<T>>());

        foreach (var tagPair in tags)
        {
            foreach (Holder<T> holder in tagPair.Value)
            {
                if (!holder.CanSerializeIn(this) || holder is not Holder<T>.Reference reference)
                    throw new InvalidOperationException($"Tag '{tagPair.Key}' contains a holder not owned by registry '{Location}'.");
                if (!tagsByHolder.TryGetValue(reference, out List<TagKey<T>>? holderTags))
                    throw new InvalidOperationException($"Tag '{tagPair.Key}' contains a reference not registered in '{Location}'.");
                holderTags.Add(tagPair.Key);
            }
        }

        foreach (var pair in tagsByHolder)
            pair.Key.BindTags(pair.Value);
    }

    public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(this, owner);
    public IEnumerator<RegistryEntry<T>> GetEnumerator() => _entriesById.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected void EnsureWritable()
    {
        if (IsFrozen) throw new InvalidOperationException($"Registry '{Location}' is frozen; no more entries can be registered.");
    }

    private sealed class RegistrationLookup : HolderGetter<T>
    {
        private readonly Registry<T> _registry;
        public RegistrationLookup(Registry<T> registry) => _registry = registry;
        public Holder<T>.Reference GetOrThrow(RegistryKey<T> key) => _registry.GetOrCreateHolderOrThrow(key);
        public HolderSet<T>.Named GetOrThrow(TagKey<T> tag) => _registry.GetOrCreateTagForRegistration(tag);
    }
}
