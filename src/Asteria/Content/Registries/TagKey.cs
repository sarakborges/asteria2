using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Names a tag within a specific registry key.
/// </summary>
public readonly record struct TagKey<T> where T : class
{
    public RegistryKey<Registry<T>> Registry { get; }
    public ResourceLocation RegistryLocation => Registry.Location;
    public ResourceLocation Location { get; }

    private TagKey(RegistryKey<Registry<T>> registry, ResourceLocation location)
    {
        if (!registry.RegistryLocation.IsValid || !registry.Location.IsValid)
        {
            throw new ArgumentException("A valid registry key is required.", nameof(registry));
        }

        if (!location.IsValid)
        {
            throw new ArgumentException("A valid tag location is required.", nameof(location));
        }

        Registry = registry;
        Location = location;
    }

    public static TagKey<T> Create(
        RegistryKey<Registry<T>> registry,
        ResourceLocation location) =>
        new(registry, location);

    public bool IsFor<TRegistry>(RegistryKey<Registry<TRegistry>> registry)
        where TRegistry : class =>
        Registry.RegistryLocation.Equals(registry.RegistryLocation) &&
        Registry.Location.Equals(registry.Location);

    public TagKey<TOther>? Cast<TOther>(RegistryKey<Registry<TOther>> registry)
        where TOther : class =>
        IsFor(registry) ? TagKey<TOther>.Create(registry, Location) : null;

    public override string ToString() =>
        $"TagKey[{Registry.Location} / {Location}]";
}
