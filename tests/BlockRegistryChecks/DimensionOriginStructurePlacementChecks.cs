using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
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
        CheckRegistryDispatchedCodec();
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

    private static void CheckRegistryDispatchedCodec()
    {
        _ = new OverworldWorld(0x5354525543545459L);
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Registry<MapCodec<StructurePlacement>> registry =
            BuiltInRegistries.StructurePlacementType;
        RegistryKey<MapCodec<StructurePlacement>>? registeredKey =
            registry.GetResourceKey(DimensionOriginStructurePlacement.PLACEMENT_CODEC);

        Require(
            registeredKey.HasValue &&
            registeredKey.Value.Location.Equals(
                ResourceLocation.FromNamespaceAndPath("minecraft", "dimension_origin")) &&
            registry.Count == 2 &&
            registry.GetResourceKey(RandomSpreadStructurePlacement.PLACEMENT_CODEC)?.Location.Equals(
                ResourceLocation.FromNamespaceAndPath("minecraft", "random_spread")) == true,
            "Only the complete minecraft:dimension_origin and minecraft:random_spread placement codecs must be registered.");

        JsonElement encoded = StructurePlacementCodecs.CODEC
            .EncodeStart(ops, DimensionOriginStructurePlacement.INSTANCE)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:dimension_origin" &&
            ReferenceEquals(
                StructurePlacementCodecs.CODEC.Parse(ops, encoded).GetOrThrow(),
                DimensionOriginStructurePlacement.INSTANCE),
            "The registry-dispatched placement codec must round-trip dimension_origin using its registered type id.");

        var spreadPlacement = new RandomSpreadStructurePlacement(
            new Vec3i(2, 3, -4),
            FrequencyReductionMethod.LEGACY_TYPE_3,
            0.625F,
            991,
            null,
            48,
            12,
            RandomSpreadType.TRIANGULAR);
        JsonElement encodedSpread = StructurePlacementCodecs.CODEC
            .EncodeStart(ops, spreadPlacement)
            .GetOrThrow();
        StructurePlacement decodedSpread = StructurePlacementCodecs.CODEC
            .Parse(ops, encodedSpread)
            .GetOrThrow();

        Require(
            ops.Get(encodedSpread, "type").GetOrThrow().GetString() == "minecraft:random_spread" &&
            ops.Get(encodedSpread, "spacing").GetOrThrow().GetInt32() == 48 &&
            ops.Get(encodedSpread, "separation").GetOrThrow().GetInt32() == 12 &&
            decodedSpread is RandomSpreadStructurePlacement decodedRandomSpread &&
            decodedRandomSpread.Spacing() == 48 &&
            decodedRandomSpread.Separation() == 12 &&
            ReferenceEquals(decodedRandomSpread.SpreadType(), RandomSpreadType.TRIANGULAR),
            "The registry-dispatched codec must round-trip random_spread through its registered type id and subtype fields.");


        JsonElement unknownType = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"),
                ops.CreateString("minecraft:concentric_rings"))
        });
        Require(
            StructurePlacementCodecs.CODEC.Parse(ops, unknownType).IsError,
            "A concentric-rings placement without its biome-search algorithm and codec must remain unregistered.");

        Require(
            StructurePlacementCodecs.CODEC.EncodeStart(
                ops,
                new UnsupportedPlacement()).IsError,
            "A placement implementation without a codec must fail encoding instead of being serialized under a false type id.");
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

    private sealed class UnsupportedPlacement : StructurePlacement
    {
        public bool IsStructureChunk(
            IStructurePlacementState state,
            int sourceX,
            int sourceZ) =>
            true;
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
