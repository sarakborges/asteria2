using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Placement;

/// <summary>
/// Registry-dispatched structure-placement codec. Only registered placement types
/// with complete codecs can be encoded or decoded.
/// </summary>
public static class StructurePlacementCodecs
{
    public static MapCodec<StructurePlacement> MAP_CODEC { get; } =
        MapCodec<StructurePlacement>.Dispatch(
            Codecs.STRING.FieldOf("type"),
            static placement =>
            {
                ArgumentNullException.ThrowIfNull(placement);

                MapCodec<StructurePlacement> placementCodec;
                try
                {
                    placementCodec = placement.Codec();
                }
                catch (NotSupportedException exception)
                {
                    return DataResult<string>.Error(() => exception.Message);
                }

                RegistryKey<MapCodec<StructurePlacement>>? key =
                    BuiltInRegistries.StructurePlacementType.GetResourceKey(placementCodec);

                return key.HasValue
                    ? DataResult<string>.Success(key.Value.Location.ToString())
                    : DataResult<string>.Error(() =>
                        $"Unregistered structure placement codec for {placement.GetType().Name}");
            },
            static type =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(type);
                if (!location.HasValue)
                {
                    return DataResult<MapCodec<StructurePlacement>>.Error(() =>
                        $"Invalid structure placement type '{type}'");
                }

                MapCodec<StructurePlacement>? codec =
                    BuiltInRegistries.StructurePlacementType.GetValue(location.Value);

                return codec is not null
                    ? DataResult<MapCodec<StructurePlacement>>.Success(codec)
                    : DataResult<MapCodec<StructurePlacement>>.Error(() =>
                        $"Unknown structure placement type '{type}'");
            });

    public static Codec<StructurePlacement> CODEC { get; } =
        MAP_CODEC.Codec();
}
