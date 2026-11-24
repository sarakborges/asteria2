using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Placement;

/// <summary>Registry dispatch for Minecraft 26.3 placement-modifier types.</summary>
public static class PlacementModifierCodecs
{
    public static Codec<PlacementModifier> CODEC { get; } = CreateCodec();

    private static Codec<PlacementModifier> CreateCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid placement modifier type key '{name}' for {BuiltInRegistries.PlacementModifierType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<PlacementModifier> dispatch =
            MapCodec<PlacementModifier>.Dispatch<ResourceLocation>(
                typeField,
                modifier =>
                {
                    MapCodec<PlacementModifier> codec = modifier.Codec();
                    RegistryKey<MapCodec<PlacementModifier>>? key =
                        BuiltInRegistries.PlacementModifierType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered placement modifier codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<PlacementModifier>? codec =
                        BuiltInRegistries.PlacementModifierType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<PlacementModifier>>.Success(codec)
                        : DataResult<MapCodec<PlacementModifier>>.Error(
                            () => $"Unknown placement modifier type '{identifier}' in {BuiltInRegistries.PlacementModifierType.Key}");
                });

        return dispatch.Codec();
    }
}
