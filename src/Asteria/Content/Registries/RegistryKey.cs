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

    public override string ToString() => $"{RegistryLocation} -> {Location}";
}
