using System.Collections.ObjectModel;

namespace Asteria.Content.Registries;

/// <summary>
/// Non-generic registry metadata used to compose heterogeneous typed lookups.
/// </summary>
public interface IRegistryLookup
{
    RegistryKey<IRegistry> RegistryKey { get; }
    Lifecycle RegistryLifecycle { get; }
}

/// <summary>
/// Immutable collection of registry lookups with typed and global lookup operations.
/// </summary>
public sealed class HolderLookupProvider : HolderGetterProvider
{
    private readonly IReadOnlyDictionary<RegistryKey<IRegistry>, IRegistryLookup> _lookups;

    private HolderLookupProvider(IEnumerable<IRegistryLookup> lookups)
    {
        ArgumentNullException.ThrowIfNull(lookups);

        var map = new Dictionary<RegistryKey<IRegistry>, IRegistryLookup>();
        foreach (IRegistryLookup lookup in lookups)
        {
            ArgumentNullException.ThrowIfNull(lookup);
            map.Add(lookup.RegistryKey, lookup);
        }

        _lookups = new ReadOnlyDictionary<RegistryKey<IRegistry>, IRegistryLookup>(map);
    }

    public static HolderLookupProvider Create(IEnumerable<IRegistryLookup> lookups) =>
        new(lookups);

    public IEnumerable<RegistryKey<IRegistry>> ListRegistryKeys() =>
        _lookups.Keys;

    public IEnumerable<IRegistryLookup> ListRegistries() =>
        _lookups.Values;

    public HolderLookup<T>.RegistryLookup? Lookup<T>(
        RegistryKey<Registry<T>> key)
        where T : class
    {
        RegistryKey<IRegistry> lookupKey = RegistryKey<IRegistry>.Create(
            RegistryKey<IRegistry>.CreateRegistryKey(key.RegistryLocation),
            key.Location);

        return _lookups.TryGetValue(lookupKey, out IRegistryLookup? lookup)
            ? lookup as HolderLookup<T>.RegistryLookup
            : null;
    }

    HolderGetter<T>? HolderGetterProvider.Lookup<T>(RegistryKey<Registry<T>> key) =>
        Lookup(key);

    public HolderLookup<T>.RegistryLookup LookupOrThrow<T>(
        RegistryKey<Registry<T>> key)
        where T : class =>
        Lookup<T>(key) ?? throw new InvalidOperationException(
            $"Registry {key.Location} not found");

    public Holder<T>.Reference? Get<T>(RegistryKey<T> key) where T : class =>
        Lookup<T>(key.GetRegistryKey())?.Get(key);

    public Holder<T>.Reference GetOrThrow<T>(RegistryKey<T> key) where T : class =>
        Get(key) ?? throw new InvalidOperationException($"Missing element {key}");

    public HolderSet<T>.Named? Get<T>(TagKey<T> key) where T : class =>
        Lookup<T>(key.Registry)?.Get(key);

    public HolderSet<T>.Named GetOrThrow<T>(TagKey<T> key) where T : class =>
        Get(key) ?? throw new InvalidOperationException($"Missing tag {key}");

    public Lifecycle AllRegistriesLifecycle()
    {
        Lifecycle lifecycle = Lifecycle.Stable();
        foreach (IRegistryLookup lookup in _lookups.Values)
            lifecycle = lifecycle.Add(lookup.RegistryLifecycle);

        return lifecycle;
    }
}
