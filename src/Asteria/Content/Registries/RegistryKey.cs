using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Identifies one value inside a specific registry.
/// The registry location and entry location are both retained, like Minecraft ResourceKey.
/// </summary>
public readonly record struct RegistryKey<T> where T : class
{
    public ResourceLocation RegistryLocation { get; }
    public ResourceLocation Location { get; }

    public RegistryKey(ResourceLocation registryLocation, ResourceLocation location)
    {
        if (!registryLocation.IsValid)
        {
            throw new ArgumentException("A valid registry location is required.", nameof(registryLocation));
        }

        if (!location.IsValid)
        {
            throw new ArgumentException("A valid entry location is required.", nameof(location));
        }

        RegistryLocation = registryLocation;
        Location = location;
    }

    public bool IsFor<TRegistry>(RegistryKey<Registry<TRegistry>> registry)
        where TRegistry : class =>
        RegistryLocation.Equals(registry.Location);

    public RegistryKey<TOther>? Cast<TOther>(RegistryKey<Registry<TOther>> registry)
        where TOther : class =>
        IsFor(registry) ? new RegistryKey<TOther>(RegistryLocation, Location) : null;

    public RegistryKey<TOther> Dependent<TOther>(
        RegistryKey<Registry<TOther>> registryKey,
        string suffix)
        where TOther : class =>
        new(registryKey.Location, Location.WithSuffix(suffix));

    public RegistryKey<TOther> Dependent<TOther>(
        RegistryKey<Registry<TOther>> registryKey,
        Func<string, string> decoration)
        where TOther : class =>
        new(registryKey.Location, Location.WithPath(decoration));

    public RegistryKey<Registry<T>> GetRegistryKey() =>
        new(ResourceLocation.Parse("root"), RegistryLocation);

    public override string ToString() =>
        $"ResourceKey[{RegistryLocation} / {Location}]";
}
