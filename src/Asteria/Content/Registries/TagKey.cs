using Asteria.Content.Identifiers;
using Asteria.Serialization;

namespace Asteria.Content.Registries;

/// <summary>
/// Names a tag within a specific registry key.
/// </summary>
public readonly record struct TagKey<T> where T : class
{
    public RegistryKey<Registry<T>> Registry { get; }
    public ResourceLocation RegistryLocation => Registry.Location;
    public ResourceLocation Location { get; }

    [Obsolete("Use TagKey<T>.Create instead.")]
    public TagKey(RegistryKey<Registry<T>> registry, ResourceLocation location)
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

#pragma warning disable CS0618
    public static TagKey<T> Create(
        RegistryKey<Registry<T>> registry,
        ResourceLocation location) =>
        new(registry, location);
#pragma warning restore CS0618

    public bool IsFor<TRegistry>(RegistryKey<Registry<TRegistry>> registry)
        where TRegistry : class =>
        Registry.RegistryLocation.Equals(registry.RegistryLocation) &&
        Registry.Location.Equals(registry.Location);

    public TagKey<TOther>? Cast<TOther>(RegistryKey<Registry<TOther>> registry)
        where TOther : class =>
        IsFor(registry) ? TagKey<TOther>.Create(registry, Location) : null;

    /// <summary>
    /// Codec for the hash-prefixed tag identifiers used by registry holder-set codecs.
    /// </summary>
    public static Codec<TagKey<T>> HashedCodec(RegistryKey<Registry<T>> registry) =>
        Codecs.STRING.FlatXMap(
            name =>
            {
                if (!name.StartsWith("#", StringComparison.Ordinal))
                    return DataResult<TagKey<T>>.Error("Not a tag id");

                ResourceLocation? location = ResourceLocation.TryParse(name[1..]);
                return location.HasValue
                    ? DataResult<TagKey<T>>.Success(Create(registry, location.Value))
                    : DataResult<TagKey<T>>.Error(() => $"Invalid tag id: {name}");
            },
            tag => tag.Location.IsValid
                ? DataResult<string>.Success($"#{tag.Location}")
                : DataResult<string>.Error("Not a tag id"));

    public override string ToString() =>
        $"TagKey[{Registry.Location} / {Location}]";
}
