using System.Collections;
using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Maps namespaced keys and runtime numeric IDs to registered objects.
/// Registration is allowed during bootstrap only; frozen registries are read-only.
/// </summary>
public sealed class Registry<T> : IReadOnlyCollection<RegistryEntry<T>> where T : class
{
    private readonly Dictionary<ResourceLocation, RegistryEntry<T>> _entriesByLocation = new();
    private readonly Dictionary<T, RegistryEntry<T>> _entriesByValue =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<RegistryEntry<T>> _entriesById = new();
    private readonly IReadOnlyList<RegistryEntry<T>> _readOnlyEntries;

    public ResourceLocation Location { get; }
    public int Count => _entriesById.Count;
    public bool IsFrozen { get; private set; }
    public IReadOnlyList<RegistryEntry<T>> Entries => _readOnlyEntries;

    public Registry(ResourceLocation location)
    {
        if (!location.IsValid)
        {
            throw new ArgumentException("A valid registry location is required.", nameof(location));
        }

        Location = location;
        _readOnlyEntries = _entriesById.AsReadOnly();
    }

    /// <summary>
    /// Registers a value and assigns the next runtime ID.
    /// Duplicate keys and registering the same object more than once are errors.
    /// </summary>
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
        var entry = new RegistryEntry<T>(key, _entriesById.Count, value);

        _entriesByLocation.Add(location, entry);
        _entriesByValue.Add(value, entry);
        _entriesById.Add(entry);

        return entry;
    }

    public T Get(ResourceLocation location) => GetEntry(location).Value;

    public RegistryEntry<T> GetEntry(ResourceLocation location)
    {
        if (_entriesByLocation.TryGetValue(location, out RegistryEntry<T>? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException(
            $"Registry '{Location}' does not contain an entry named '{location}'.");
    }

    public bool TryGetEntry(ResourceLocation location, out RegistryEntry<T>? entry) =>
        _entriesByLocation.TryGetValue(location, out entry);

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

    public int GetId(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry) ? entry.Id : -1;
    }

    public ResourceLocation? GetKey(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _entriesByValue.TryGetValue(value, out RegistryEntry<T>? entry)
            ? entry.Key.Location
            : null;
    }

    public RegistryEntry<T> ById(int id)
    {
        if ((uint)id >= (uint)_entriesById.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "The runtime registry ID does not exist.");
        }

        return _entriesById[id];
    }

    /// <summary>
    /// Ends registration. Calling Freeze more than once is harmless.
    /// </summary>
    public void Freeze() => IsFrozen = true;

    public IEnumerator<RegistryEntry<T>> GetEnumerator() => _entriesById.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureWritable()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException(
                $"Registry '{Location}' is frozen; no more entries can be registered.");
        }
    }
}
