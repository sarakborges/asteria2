using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Vanilla-style built-in registration helpers.
/// </summary>
public static class Registry
{
    public static T Register<T>(Registry<T> registry, string name, T value)
        where T : class =>
        Register(registry, ResourceLocation.Parse(name), value);

    public static T Register<T>(Registry<T> registry, ResourceLocation location, T value)
        where T : class =>
        Register(registry, RegistryKey<T>.Create(registry.Key, location), value);

    public static T Register<T>(Registry<T> registry, RegistryKey<T> key, T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        registry.Register(key, value, RegistrationInfo.BuiltIn);
        return value;
    }

    public static Holder<T>.Reference RegisterForHolder<T>(
        Registry<T> registry,
        ResourceLocation location,
        T value)
        where T : class =>
        RegisterForHolder(registry, RegistryKey<T>.Create(registry.Key, location), value);

    public static Holder<T>.Reference RegisterForHolder<T>(
        Registry<T> registry,
        RegistryKey<T> key,
        T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        return registry.Register(key, value, RegistrationInfo.BuiltIn);
    }
}
