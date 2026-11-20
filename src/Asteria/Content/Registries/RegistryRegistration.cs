using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Vanilla-style built-in registration helpers.
/// </summary>
public static class Registry
{
    public static TValue Register<TRegistryValue, TValue>(
        Registry<TRegistryValue> registry,
        string name,
        TValue value)
        where TRegistryValue : class
        where TValue : TRegistryValue =>
        Register(registry, ResourceLocation.Parse(name), value);

    public static TValue Register<TRegistryValue, TValue>(
        Registry<TRegistryValue> registry,
        ResourceLocation location,
        TValue value)
        where TRegistryValue : class
        where TValue : TRegistryValue
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        return Register(registry, RegistryKey<TRegistryValue>.Create(registry.Key, location), value);
    }

    public static TValue Register<TRegistryValue, TValue>(
        Registry<TRegistryValue> registry,
        RegistryKey<TRegistryValue> key,
        TValue value)
        where TRegistryValue : class
        where TValue : TRegistryValue
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        registry.Register(key, value, RegistrationInfo.BuiltIn);
        return value;
    }

    public static Holder<TRegistryValue>.Reference RegisterForHolder<TRegistryValue, TValue>(
        Registry<TRegistryValue> registry,
        ResourceLocation location,
        TValue value)
        where TRegistryValue : class
        where TValue : TRegistryValue
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        return RegisterForHolder(
            registry,
            RegistryKey<TRegistryValue>.Create(registry.Key, location),
            value);
    }

    public static Holder<TRegistryValue>.Reference RegisterForHolder<TRegistryValue, TValue>(
        Registry<TRegistryValue> registry,
        RegistryKey<TRegistryValue> key,
        TValue value)
        where TRegistryValue : class
        where TValue : TRegistryValue
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(value);

        return registry.Register(key, value, RegistrationInfo.BuiltIn);
    }
}
