using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Defaulted MappedRegistry lookup semantics: unresolved IDs and values map to the configured fallback.
/// Optional/exact holder lookups remain exact.
/// </summary>
public sealed class DefaultedRegistry<T> : Registry<T> where T : class
{
    private RegistryEntry<T>? _defaultEntry;

    public ResourceLocation DefaultKey { get; }

    public DefaultedRegistry(
        ResourceLocation location,
        ResourceLocation defaultKey,
        bool allowIntrusiveHolders = false)
        : base(location, allowIntrusiveHolders)
    {
        if (!defaultKey.IsValid)
        {
            throw new ArgumentException("A valid default key is required.", nameof(defaultKey));
        }

        DefaultKey = defaultKey;
    }

    public override T Get(ResourceLocation location) => GetEntry(location).Value;

    public override RegistryEntry<T> GetEntry(ResourceLocation location)
    {
        if (TryGetEntry(location, out RegistryEntry<T>? entry))
        {
            return entry!;
        }

        return GetDefaultEntry();
    }

    public override int GetId(T value)
    {
        int id = base.GetId(value);
        return id >= 0 ? id : GetDefaultEntry().Id;
    }

    public override ResourceLocation? GetKey(T value) =>
        base.GetKey(value) ?? GetDefaultEntry().Key.Location;

    public override RegistryEntry<T> ById(int id) =>
        (uint)id < (uint)Count ? base.ById(id) : GetDefaultEntry();

    public override void Freeze()
    {
        if (!TryGetEntry(DefaultKey, out RegistryEntry<T>? entry))
        {
            throw new InvalidOperationException(
                $"Cannot freeze registry '{Location}': default entry '{DefaultKey}' was not registered.");
        }

        _defaultEntry = entry;
        base.Freeze();
    }

    private RegistryEntry<T> GetDefaultEntry()
    {
        if (_defaultEntry is not null)
        {
            return _defaultEntry;
        }

        if (TryGetEntry(DefaultKey, out RegistryEntry<T>? entry))
        {
            return entry!;
        }

        throw new InvalidOperationException(
            $"Default entry '{DefaultKey}' has not been registered in '{Location}'.");
    }
}
