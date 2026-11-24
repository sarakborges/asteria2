using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Registry-dispatched codec surface for Minecraft 26.3 world carvers.</summary>
public static class WorldCarvers
{
    public static Codec<WorldCarver> DIRECT_CODEC { get; } = CreateCodec();

    private static Codec<WorldCarver> CreateCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.CarverType.Key}");
            },
            identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<WorldCarver> dispatch =
            MapCodec<WorldCarver>.Dispatch<ResourceLocation>(
                typeField,
                carver =>
                {
                    MapCodec<WorldCarver> codec = carver.GetCodec();
                    RegistryKey<MapCodec<WorldCarver>>? key =
                        BuiltInRegistries.CarverType.GetResourceKey(codec);
                    return key.HasValue
                        ? DataResult<ResourceLocation>.Success(key.Value.Location)
                        : DataResult<ResourceLocation>.Error(
                            () => $"Unregistered world carver codec '{codec}'");
                },
                identifier =>
                {
                    MapCodec<WorldCarver>? codec =
                        BuiltInRegistries.CarverType.GetValue(identifier);
                    return codec is not null
                        ? DataResult<MapCodec<WorldCarver>>.Success(codec)
                        : DataResult<MapCodec<WorldCarver>>.Error(
                            () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.CarverType.Key}");
                });

        return dispatch.Codec();
    }
}
