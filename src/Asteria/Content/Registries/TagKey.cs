using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Names a tag within a specific registry.
/// </summary>
public readonly record struct TagKey<T> where T : class
{
    public ResourceLocation RegistryLocation { get; }
    public ResourceLocation Location { get; }

    public TagKey(ResourceLocation registryLocation, ResourceLocation location)
    {
        if (!registryLocation.IsValid)
        {
            throw new ArgumentException("A valid registry location is required.", nameof(registryLocation));
        }

        if (!location.IsValid)
        {
            throw new ArgumentException("A valid tag location is required.", nameof(location));
        }

        RegistryLocation = registryLocation;
        Location = location;
    }

    public bool IsFor<TRegistry>(RegistryKey<Registry<TRegistry>> registry)
        where TRegistry : class =>
        RegistryLocation.Equals(registry.Location);

    public TagKey<TOther>? Cast<TOther>(RegistryKey<Registry<TOther>> registry)
        where TOther : class =>
        IsFor(registry) ? new TagKey<TOther>(RegistryLocation, Location) : null;

    public override string ToString() =>
        $"TagKey[{RegistryLocation} / {Location}]";
}
