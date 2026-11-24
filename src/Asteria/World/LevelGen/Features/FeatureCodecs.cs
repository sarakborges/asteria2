using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Features;

/// <summary>Registry-dispatched and holder codecs for Minecraft 26.3 features.</summary>
public static class FeatureCodecs
{
    public static Codec<Feature> DIRECT_CODEC { get; } = CreateDirectCodec();

    public static Codec<Holder<Feature>> CODEC { get; } =
        RegistryCodecs.Holder(Registries.FEATURE, DIRECT_CODEC);

    public static Codec<HolderSet<Feature>> LIST_CODEC { get; } =
        RegistryCodecs.HolderSet(Registries.FEATURE);

    private static Codec<Feature> CreateDirectCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid feature type key '{name}' for {BuiltInRegistries.FeatureType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<Feature> dispatch =
            MapCodec<Feature>.Dispatch<ResourceLocation>(
                typeField,
                feature =>
                {
                    MapCodec<Feature> codec = feature.Codec();
                    RegistryKey<MapCodec<Feature>>? key =
                        BuiltInRegistries.FeatureType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered feature codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<Feature>? codec =
                        BuiltInRegistries.FeatureType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<Feature>>.Success(codec)
                        : DataResult<MapCodec<Feature>>.Error(
                            () => $"Unknown feature type '{identifier}' in {BuiltInRegistries.FeatureType.Key}");
                });

        return dispatch.Codec();
    }
}
