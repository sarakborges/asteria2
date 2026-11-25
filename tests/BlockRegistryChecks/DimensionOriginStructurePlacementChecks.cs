using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Placement;

internal static class DimensionOriginStructurePlacementChecks
{
    public static void Run()
    {
        CheckSingletonAndCodec();
        CheckOnlyTheDimensionOriginIsSelected();
        CheckDefaultLocatePosition();
    }

    private static void CheckSingletonAndCodec()
    {
        Require(
            ReferenceEquals(
                DimensionOriginStructurePlacement.INSTANCE,
                DimensionOriginStructurePlacement.INSTANCE),
            "DimensionOriginStructurePlacement must expose one canonical singleton.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement encoded = DimensionOriginStructurePlacement.MAP_CODEC
            .EncodeStart(ops, DimensionOriginStructurePlacement.INSTANCE)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            ReferenceEquals(
                DimensionOriginStructurePlacement.MAP_CODEC.Parse(ops, encoded).GetOrThrow(),
                DimensionOriginStructurePlacement.INSTANCE),
            "The dimension-origin unit codec must encode an empty map and decode to the canonical singleton.");
    }

    private static void CheckOnlyTheDimensionOriginIsSelected()
    {
        const long seed = 88442211L;
        ChunkPos origin = new(-17, 29);
        IStructurePlacementState state = CreateState(seed, origin);

        Require(
            DimensionOriginStructurePlacement.INSTANCE.IsStructureChunk(
                state,
                origin.X,
                origin.Z),
            "The dimension-origin placement must accept the exact configured origin chunk.");
        Require(
            !DimensionOriginStructurePlacement.INSTANCE.IsStructureChunk(
                state,
                origin.X + 1,
                origin.Z) &&
            !DimensionOriginStructurePlacement.INSTANCE.IsStructureChunk(
                state,
                origin.X,
                origin.Z - 1),
            "The dimension-origin placement must reject every chunk whose X or Z differs from the origin.");
    }

    private static void CheckDefaultLocatePosition()
    {
        BlockPos locate = DimensionOriginStructurePlacement.INSTANCE.GetLocatePos(
            new ChunkPos(-3, 5));

        Require(
            locate.GetX() == -48 &&
            locate.GetY() == 0 &&
            locate.GetZ() == 80 &&
            DimensionOriginStructurePlacement.INSTANCE.LocateOffset().Equals(Vec3i.Zero),
            "Dimension-origin placement must retain the default zero locate offset and chunk-corner position.");
    }

    private static IStructurePlacementState CreateState(long seed, ChunkPos origin)
    {
        var world = new OverworldWorld(seed);
        var parameters = new Climate.ParameterList<Holder<Biome>>(
            new[]
            {
                new Pair<Climate.ParameterPoint, Holder<Biome>>(
                    Climate.Parameters(0, 0, 0, 0, 0, 0, 0),
                    Biomes.PLAINS.BuiltInRegistryHolder)
            });
        var biomeSource = new MultiNoiseBiomeSource(parameters);

        return ChunkGeneratorStructureState.CreateForNormal(
            world.Generator.RandomState,
            seed,
            origin,
            biomeSource,
            Array.Empty<Holder<StructureSet>>());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
