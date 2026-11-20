using System.Collections;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// MappedRegistry-style registry. All key, value, location, and numeric-ID indexes point
/// directly to stable Holder.Reference instances.
/// </summary>
public class Registry<T> : IEnumerable<T>, IIdMap<T>, HolderLookup<T>.RegistryLookup where T : class
{
    private static readonly ResourceLocation RootRegistryLocation = ResourceLocation.Parse("root");

    private readonly Dictionary<ResourceLocation, Holder<T>.Reference> _byLocation = new();
    private readonly Dictionary<RegistryKey<T>, Holder<T>.Reference> _byKey = new();
    private readonly Dictionary<T, Holder<T>.Reference> _byValue =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<T, int> _toId =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RegistryKey<T>, RegistrationInfo> _registrationInfos = new();
    private readonly List<Holder<T>.Reference> _byId = new();
    private readonly IReadOnlyList<Holder<T>.Reference> _readOnlyEntries;
    private Dictionary<T, Holder<T>.Reference>? _unregisteredIntrusiveHolders;
    private Dictionary<TagKey<T>, HolderSet<T>.Named> _registrationTags = new();
    private IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named>? _boundTags;

    public ResourceLocation Location { get; }
    public RegistryKey<Registry<T>> Key { get; }
    public int Count => _byKey.Count;
    public bool IsFrozen { get; protected set; }
    public Lifecycle RegistryLifecycle { get; private set; }
    public IReadOnlyList<Holder<T>.Reference> Entries => _readOnlyEntries;

    public Registry(
        ResourceLocation location,
        bool allowIntrusiveHolders = false,
        Lifecycle? initialLifecycle = null)
    {
        if (!location.IsValid)
            throw new ArgumentException("A valid registry location is required.", nameof(location));

        Location = location;
        Key = new RegistryKey<Registry<T>>(RootRegistryLocation, location);
        RegistryLifecycle = initialLifecycle ?? Lifecycle.Stable();
        _unregisteredIntrusiveHolders = allowIntrusiveHolders
            ? new Dictionary<T, Holder<T>.Reference>(ReferenceEqualityComparer.Instance)
            : null;
        _readOnlyEntries = _byId.AsReadOnly();
    }

    public virtual Holder<T>.Reference Register(ResourceLocation location, T value) =>
        Register(
            new RegistryKey<T>(Location, location),
            value,
            RegistrationInfo.BuiltIn);

    public virtual Holder<T>.Reference Register(
        RegistryKey<T> key,
        T value,
        RegistrationInfo registrationInfo)
    {
        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(registrationInfo);

        if (!key.Location.IsValid)
            throw new ArgumentException("A valid entry location is required.", nameof(key));
        if (_byLocation.ContainsKey(key.Location))
            throw new InvalidOperationException($"Adding duplicate key '{key}' to registry '{Location}'.");
        if (_byValue.ContainsKey(value))
            throw new InvalidOperationException($"Adding duplicate value '{value}' to registry '{Location}'.");

        Holder<T>.Reference holder;
        if (_unregisteredIntrusiveHolders is not null)
        {
            if (!_unregisteredIntrusiveHolders.Remove(value, out Holder<T>.Reference? intrusiveHolder))
                throw new InvalidOperationException($"Missing intrusive holder for {key}:{value}.");

            holder = intrusiveHolder;
            holder.BindKey(key);
        }
        else if (!_byKey.TryGetValue(key, out holder!))
        {
            holder = Holder<T>.Reference.CreateStandAlone(this, key);
        }

        int id = _byId.Count;
        _byKey[key] = holder;
        _byLocation.Add(key.Location, holder);
        _byValue.Add(value, holder);
        _toId.Add(value, id);
        _byId.Add(holder);
        _registrationInfos.Add(key, registrationInfo);
        RegistryLifecycle = RegistryLifecycle.Add(registrationInfo.Lifecycle);
        return holder;
    }

    public RegistrationInfo? GetRegistrationInfo(RegistryKey<T> key) =>
        _registrationInfos.TryGetValue(key, out RegistrationInfo? info) ? info : null;

    public Holder<T>.Reference CreateIntrusiveHolder(T value)
    {
        if (_unregisteredIntrusiveHolders is null)
            throw new InvalidOperationException("This registry can't create intrusive holders.");

        EnsureWritable();
        ArgumentNullException.ThrowIfNull(value);

        if (_unregisteredIntrusiveHolders.TryGetValue(value, out Holder<T>.Reference? existing))
            return existing;

        Holder<T>.Reference holder = Holder<T>.Reference.CreateIntrusive(this, value);
        _unregisteredIntrusiveHolders.Add(value, holder);
        return holder;
    }

    public virtual T Get(ResourceLocation location) =>
        GetValue(location) ?? throw new KeyNotFoundException(
            $"Registry '{Location}' does not contain an entry named '{location}'.");

    public virtual T? GetValue(ResourceLocation location) =>
        _byLocation.TryGetValue(location, out Holder<T>.Reference? holder)
            ? holder.Value
            : null;

    public T? GetValue(RegistryKey<T> key) =>
        _byKey.TryGetValue(key, out Holder<T>.Reference? holder)
            ? holder.Value
            : null;

    public T GetValueOrThrow(RegistryKey<T> key) =>
        GetValue(key) ?? throw new InvalidOperationException(
            $"Missing key in registry '{Location}': '{key}'.");

    public T? GetOptional(ResourceLocation location) =>
        _byLocation.TryGetValue(location, out Holder<T>.Reference? holder)
            ? holder.Value
            : null;

    public T? GetOptional(RegistryKey<T> key) =>
        _byKey.TryGetValue(key, out Holder<T>.Reference? holder)
            ? holder.Value
            : null;

    public RegistryKey<T>? GetResourceKey(T? value) =>
        value is not null && _byValue.TryGetValue(value, out Holder<T>.Reference? holder)
            ? holder.Key
            : null;

    public bool ContainsKey(ResourceLocation location) => _byLocation.ContainsKey(location);

    public bool ContainsKey(RegistryKey<T> key) => _byKey.ContainsKey(key);

    Holder<T>.Reference? HolderGetter<T>.Get(RegistryKey<T> key) =>
        GetHolder(key);

    HolderSet<T>.Named? HolderGetter<T>.Get(TagKey<T> tag) =>
        GetTag(tag);

    public Holder<T>.Reference? GetHolder(ResourceLocation location) =>
        _byLocation.TryGetValue(location, out Holder<T>.Reference? holder) ? holder : null;

    public Holder<T>.Reference? GetHolder(RegistryKey<T> key) =>
        _byKey.TryGetValue(key, out Holder<T>.Reference? holder) ? holder : null;

    public Holder<T>.Reference GetHolderOrThrow(RegistryKey<T> key) =>
        GetHolder(key) ?? throw new KeyNotFoundException(
            $"Registry '{Location}' does not contain key '{key}'.");

    public virtual Holder<T>.Reference? GetHolderById(int id) =>
        (uint)id < (uint)_byId.Count ? _byId[id] : null;

    public virtual Holder<T>.Reference? GetAny() =>
        _byId.Count == 0 ? null : _byId[0];

    public virtual Holder<T>.Reference? GetRandom(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return _byId.Count == 0 ? null : _byId[random.Next(_byId.Count)];
    }

    public bool IsEmpty() => _byKey.Count == 0;

    public IReadOnlyCollection<ResourceLocation> KeySet() => _byLocation.Keys;

    public IReadOnlyCollection<RegistryKey<T>> RegistryKeySet() => _byKey.Keys;

    public IEnumerable<KeyValuePair<RegistryKey<T>, T>> EntrySet()
    {
        foreach (KeyValuePair<RegistryKey<T>, Holder<T>.Reference> pair in _byKey)
        {
            yield return new KeyValuePair<RegistryKey<T>, T>(pair.Key, pair.Value.Value);
        }
    }

    public IEnumerable<Holder<T>.Reference> ListElements() => _readOnlyEntries;

    public IEnumerable<HolderSet<T>.Named> ListTags() =>
        (_boundTags ?? throw new InvalidOperationException("Tags not bound.")).Values;

    public HolderSet<T>.Named? GetTag(TagKey<T> tag)
    {
        IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> tags =
            _boundTags ?? throw new InvalidOperationException("Tags not bound.");
        return tags.TryGetValue(tag, out HolderSet<T>.Named? holders) ? holders : null;
    }

    public HolderSet<T> GetTagOrEmpty(TagKey<T> tag) => GetTag(tag) ?? HolderSet<T>.Empty;

    public Holder<T> WrapAsHolder(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _byValue.TryGetValue(value, out Holder<T>.Reference? holder)
            ? holder
            : Holder<T>.Direct(value);
    }

    public HolderGetter<T> CreateRegistrationLookup()
    {
        EnsureWritable();
        return new RegistrationLookup(this);
    }

    private Holder<T>.Reference GetOrCreateHolderOrThrow(RegistryKey<T> key)
    {
        if (_byKey.TryGetValue(key, out Holder<T>.Reference? registered))
            return registered;

        if (_unregisteredIntrusiveHolders is not null)
            throw new InvalidOperationException("This registry can't create new holders without a value.");

        EnsureWritable();
        Holder<T>.Reference holder = Holder<T>.Reference.CreateStandAlone(this, key);
        _byKey.Add(key, holder);
        return holder;
    }

    private HolderSet<T>.Named GetOrCreateTagForRegistration(TagKey<T> tag)
    {
        if (!_registrationTags.TryGetValue(tag, out HolderSet<T>.Named? holders))
        {
            holders = new HolderSet<T>.Named(this, tag);
            _registrationTags.Add(tag, holders);
        }

        return holders;
    }

    public IIdMap<Holder<T>> AsHolderIdMap() => new HolderIdMap(this);

    private sealed class HolderIdMap : IIdMap<Holder<T>>
    {
        private readonly Registry<T> _registry;

        public HolderIdMap(Registry<T> registry) => _registry = registry;

        public int Count => _registry.Count;

        public int GetId(Holder<T>? holder) =>
            holder is null ? -1 : _registry.GetId(holder.Value);

        public Holder<T>? ById(int id) => _registry.GetHolderById(id);

        public IEnumerator<Holder<T>> GetEnumerator() =>
            _registry.ListElements().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public virtual int GetId(T? value) =>
        value is not null && _toId.TryGetValue(value, out int id) ? id : -1;

    public virtual T? ById(int id) =>
        (uint)id < (uint)_byId.Count ? _byId[id].Value : null;

    public virtual T GetById(int id) =>
        ById(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "The runtime registry ID does not exist.");

    public virtual ResourceLocation? GetKey(T? value) =>
        value is not null && _byValue.TryGetValue(value, out Holder<T>.Reference? holder)
            ? holder.Key.Location
            : null;

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

    public PendingTagReload<T> PrepareTagReload(
        IEnumerable<KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>>> tagDefinitions)
    {
        if (!IsFrozen)
            throw new InvalidOperationException("Tag reload is only valid after registry freeze.");
        ArgumentNullException.ThrowIfNull(tagDefinitions);

        var pending = new Dictionary<TagKey<T>, (HolderSet<T>.Named Set, Holder<T>[] Values)>();
        foreach (KeyValuePair<TagKey<T>, IEnumerable<Holder<T>>> definition in tagDefinitions)
        {
            TagKey<T> key = definition.Key;
            HolderSet<T>.Named set = _registrationTags.TryGetValue(key, out HolderSet<T>.Named? existing)
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
        if (IsFrozen)
            return;

        IsFrozen = true;
        foreach (var pair in _byValue)
            pair.Value.BindValue(pair.Key);

        RegistryKey<T>[] unboundEntries = _byKey
            .Where(pair => !pair.Value.IsBound)
            .Select(pair => pair.Key)
            .ToArray();
        if (unboundEntries.Length > 0)
            throw new InvalidOperationException(
                $"Unbound values in registry '{Location}': {string.Join(", ", unboundEntries.Select(key => key.Location))}");

        if (_unregisteredIntrusiveHolders is not null)
        {
            if (_unregisteredIntrusiveHolders.Count != 0)
                throw new InvalidOperationException(
                    $"Some intrusive holders were not registered: {_unregisteredIntrusiveHolders.Count}");

            _unregisteredIntrusiveHolders = null;
        }

        foreach (HolderSet<T>.Named tag in _registrationTags.Values)
            if (!tag.IsBound)
                throw new InvalidOperationException($"Unbound tags in registry '{Location}'.");

        var frozenTags = new Dictionary<TagKey<T>, HolderSet<T>.Named>(_registrationTags);
        _boundTags = frozenTags;
        RefreshTagsInHolders(frozenTags);
    }

    private void RefreshTagsInHolders(IReadOnlyDictionary<TagKey<T>, HolderSet<T>.Named> tags)
    {
        var tagsByHolder = new Dictionary<Holder<T>.Reference, List<TagKey<T>>>(
            ReferenceEqualityComparer.Instance);
        foreach (Holder<T>.Reference holder in _byId)
            tagsByHolder.Add(holder, new List<TagKey<T>>());

        foreach (var tagPair in tags)
        {
            foreach (Holder<T> holder in tagPair.Value)
            {
                if (!holder.CanSerializeIn(this) || holder is not Holder<T>.Reference reference)
                    throw new InvalidOperationException(
                        $"Tag '{tagPair.Key}' contains a holder not owned by registry '{Location}'.");
                if (!tagsByHolder.TryGetValue(reference, out List<TagKey<T>>? holderTags))
                    throw new InvalidOperationException(
                        $"Tag '{tagPair.Key}' contains a reference not registered in '{Location}'.");
                holderTags.Add(tagPair.Key);
            }
        }

        foreach (var pair in tagsByHolder)
            pair.Key.BindTags(pair.Value);
    }

    public bool CanSerializeIn(HolderOwner<T> owner) => ReferenceEquals(this, owner);

    public override string ToString() => $"Registry[{Key} ({RegistryLifecycle})]";

    public IEnumerator<T> GetEnumerator()
    {
        foreach (Holder<T>.Reference holder in _byId)
            yield return holder.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected void EnsureWritable()
    {
        if (IsFrozen)
            throw new InvalidOperationException($"Registry '{Location}' is frozen; no more entries can be registered.");
    }

    private sealed class RegistrationLookup : HolderGetter<T>
    {
        private readonly Registry<T> _registry;
        public RegistrationLookup(Registry<T> registry) => _registry = registry;

        public Holder<T>.Reference? Get(RegistryKey<T> key) =>
            _registry.GetOrCreateHolderOrThrow(key);

        public HolderSet<T>.Named? Get(TagKey<T> tag) =>
            _registry.GetOrCreateTagForRegistration(tag);

        public bool CanSerializeIn(HolderOwner<T> owner) =>
            _registry.CanSerializeIn(owner);
    }
}
