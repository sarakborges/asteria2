namespace Asteria.Content.Registries;

/// <summary>
/// Cross-registry element and tag lookup surface.
/// </summary>
public interface HolderGetterProvider
{
    HolderGetter<T>? Lookup<T>(RegistryKey<Registry<T>> key) where T : class;

    HolderGetter<T> LookupOrThrow<T>(RegistryKey<Registry<T>> key) where T : class =>
        Lookup<T>(key) ?? throw new InvalidOperationException(
            $"Registry {key.Location} not found");

    Holder<T>.Reference? Get<T>(RegistryKey<T> key) where T : class =>
        Lookup<T>(key.GetRegistryKey())?.Get(key);

    Holder<T>.Reference GetOrThrow<T>(RegistryKey<T> key) where T : class =>
        Get(key) ?? throw new InvalidOperationException($"Missing element {key}");

    HolderSet<T>.Named? Get<T>(TagKey<T> key) where T : class =>
        Lookup<T>(key.Registry)?.Get(key);

    HolderSet<T>.Named GetOrThrow<T>(TagKey<T> key) where T : class =>
        Get(key) ?? throw new InvalidOperationException($"Missing tag {key}");
}
