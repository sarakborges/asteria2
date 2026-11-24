using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Placement;

internal static class PlacementModifierChecks
{
    private sealed class ProbeFeature : Feature
    {
        public List<BlockPos> Positions { get; } = new();

        public MapCodec<Feature> Codec() =>
            throw new NotSupportedException("ProbeFeature is not serialized.");

        public bool Place(
            OverworldWorld level,
            NoiseBasedChunkGenerator generator,
            IRandomSource random,
            BlockPos origin)
        {
            Positions.Add(origin);
            return true;
        }
    }

    public static void Run()
    {
        CheckTypeRegistry();
        CheckModifierCodecs();
        CheckInSquarePlacement();
        CheckRarityFilter();
        CheckCountPlacement();
        CheckBiomeFilter();
        CheckFeaturePlacerOrder();
    }

    private static void CheckTypeRegistry()
    {
        Require(
            BuiltInRegistries.PlacementModifierType.Count == 4,
            "The placement modifier registry must contain the four implemented algorithms.");

        foreach (string id in new[] { "in_square", "biome", "rarity_filter", "count" })
        {
            Require(
                BuiltInRegistries.PlacementModifierType.ContainsKey(
                    ResourceLocation.Parse($"asteria:{id}")),
                $"The placement modifier registry must expose asteria:{id}.");
        }
    }

    private static void CheckModifierCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        PlacementModifier inSquare = PlacementModifierCodecs.CODEC
            .Parse(ops, ParseJson("""{"type":"asteria:in_square"}"""))
            .GetOrThrow();
        Require(
            ReferenceEquals(inSquare, InSquarePlacement.INSTANCE),
            "The in_square dispatch codec must decode to the canonical singleton.");

        PlacementModifier biome = PlacementModifierCodecs.CODEC
            .Parse(ops, ParseJson("""{"type":"asteria:biome"}"""))
            .GetOrThrow();
        Require(
            ReferenceEquals(biome, BiomeFilter.INSTANCE),
            "The biome dispatch codec must decode to the canonical singleton.");

        PlacementModifier rarity = PlacementModifierCodecs.CODEC
            .Parse(ops, ParseJson("""{"type":"asteria:rarity_filter","chance":9}"""))
            .GetOrThrow();
        Require(
            rarity is RarityFilter { Chance: 9 },
            "The rarity_filter dispatch codec must decode its chance field.");

        PlacementModifier count = PlacementModifierCodecs.CODEC
            .Parse(ops, ParseJson("""{"type":"asteria:count","count":3}"""))
            .GetOrThrow();
        Require(
            count is CountPlacement { CountProvider: ConstantInt { Value: 3 } },
            "The count dispatch codec must decode the constant IntProvider shorthand.");

        JsonElement encoded = PlacementModifierCodecs.CODEC
            .EncodeStart(ops, CountPlacement.Of(4))
            .GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:count" &&
            ops.Get(encoded, "count").GetOrThrow().GetInt32() == 4,
            "The count dispatch codec must encode the Asteria placement type and numeric constant shorthand.");

        Require(
            PlacementModifierCodecs.CODEC
                .Parse(ops, ParseJson("""{"type":"asteria:missing"}""")).IsError,
            "The placement modifier dispatch codec must reject unknown type identifiers.");
    }

    private static void CheckInSquarePlacement()
    {
        var context = CreatePlacementContext(topFeature: null);
        var origin = new BlockPos(-31, 72, 47);
        var expectedRandom = new LegacyRandomSource(123456L);
        var actualRandom = new LegacyRandomSource(123456L);
        BlockPos? actual = null;

        InSquarePlacement.INSTANCE.Modify(
            context,
            actualRandom,
            origin,
            position => actual = position);

        int expectedX = expectedRandom.NextInt(16) + origin.GetX();
        int expectedZ = expectedRandom.NextInt(16) + origin.GetZ();

        Require(
            actual is not null &&
            actual.GetX() == expectedX &&
            actual.GetY() == origin.GetY() &&
            actual.GetZ() == expectedZ,
            "InSquarePlacement must consume X then Z bounded draws and preserve Y.");
    }

    private static void CheckRarityFilter()
    {
        var context = CreatePlacementContext(topFeature: null);
        var origin = new BlockPos(3, 64, 9);
        const int chance = 7;
        const long seed = 998877L;

        var expectedRandom = new LegacyRandomSource(seed);
        bool expected = expectedRandom.NextFloat() < 1.0f / chance;
        bool actual = RarityFilter.OnAverageOnceEvery(chance).ShouldPlace(
            context,
            new LegacyRandomSource(seed),
            origin);

        Require(
            actual == expected,
            "RarityFilter must consume exactly one float and compare it with 1/chance.");

        Require(
            RarityFilter.MAP_CODEC
                .Parse(JsonOps.INSTANCE, ParseJson("""{"chance":7}"""))
                .GetOrThrow().Chance == 7,
            "RarityFilter must decode the positive chance field.");

        Require(
            RarityFilter.MAP_CODEC
                .Parse(JsonOps.INSTANCE, ParseJson("""{"chance":0}""")).IsError,
            "RarityFilter must reject a zero chance.");
    }

    private static void CheckCountPlacement()
    {
        var placement = CountPlacement.Of(UniformInt.Of(2, 5));
        var expectedRandom = new LegacyRandomSource(334455L);
        int expectedCount = UniformInt.Of(2, 5).Sample(expectedRandom);
        int actualCount = placement.Count(
            new LegacyRandomSource(334455L),
            new BlockPos(0, 0, 0));

        Require(
            actualCount == expectedCount,
            "CountPlacement must delegate to its IntProvider with no additional RNG calls.");

        Require(
            CountPlacement.MAP_CODEC
                .Parse(
                    JsonOps.INSTANCE,
                    ParseJson("""{"count":2}"""))
                .GetOrThrow().CountProvider.Equals(ConstantInt.Of(2)),
            "CountPlacement must serialize its IntProvider under the count field.");

        Require(
            CountPlacement.MAP_CODEC
                .Parse(JsonOps.INSTANCE, ParseJson("""{"count":4097}""")).IsError,
            "CountPlacement must enforce its inclusive 0..4096 provider bounds.");
    }

    private static void CheckBiomeFilter()
    {
        var world = new OverworldWorld(
            seed: 0x504C4143454D4F44L,
            LevelHeightAccessor.Create(-64, 96));
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(world.Seed);
        var origin = new BlockPos(2, 40, 3);
        var feature = new ProbeFeature();
        var placed = new PlacedFeature(
            Holder<Feature>.Direct(feature),
            [BiomeFilter.INSTANCE]);

        bool rejected = BiomeFilter.INSTANCE.ShouldPlace(
            CreatePlacementContext(world, generator, placed),
            new LegacyRandomSource(1L),
            origin);

        Require(
            !rejected,
            "BiomeFilter must reject a top-level placed feature absent from the origin biome's generation settings.");

        bool missingTopFeatureRejected = false;
        try
        {
            _ = BiomeFilter.INSTANCE.ShouldPlace(
                CreatePlacementContext(world, generator, topFeature: null),
                new LegacyRandomSource(1L),
                origin);
        }
        catch (InvalidOperationException exception)
        {
            missingTopFeatureRejected = exception.Message.Contains(
                "biome check an unregistered feature",
                StringComparison.Ordinal);
        }

        Require(
            missingTopFeatureRejected,
            "BiomeFilter must fail when invoked without a biome-checked top-level PlacedFeature.");
    }

    private static void CheckFeaturePlacerOrder()
    {
        const long seed = 11223344L;
        var world = new OverworldWorld(
            seed,
            LevelHeightAccessor.Create(-64, 96));
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(seed);
        var feature = new ProbeFeature();
        var placed = new PlacedFeature(
            Holder<Feature>.Direct(feature),
            [
                CountPlacement.Of(2),
                InSquarePlacement.INSTANCE
            ]);
        var origin = new BlockPos(-24, 82, 37);
        var expectedRandom = new LegacyRandomSource(seed);
        var actualRandom = new LegacyRandomSource(seed);

        var featurePlacer = new FeaturePlacer(world, generator);
        bool placedAny = featurePlacer.Place(placed, actualRandom, origin);

        BlockPos[] expected =
        [
            new(
                origin.GetX() + expectedRandom.NextInt(16),
                origin.GetY(),
                origin.GetZ() + expectedRandom.NextInt(16)),
            new(
                origin.GetX() + expectedRandom.NextInt(16),
                origin.GetY(),
                origin.GetZ() + expectedRandom.NextInt(16))
        ];

        Require(
            placedAny &&
            feature.Positions.Count == 2 &&
            feature.Positions.Zip(expected).All(pair =>
                pair.First.GetX() == pair.Second.GetX() &&
                pair.First.GetY() == pair.Second.GetY() &&
                pair.First.GetZ() == pair.Second.GetZ()),
            "FeaturePlacer must preserve the vanilla LIFO modifier traversal and the X-then-Z RNG order.");

        feature.Positions.Clear();
        var biomeChecked = new PlacedFeature(
            Holder<Feature>.Direct(feature),
            [BiomeFilter.INSTANCE]);
        bool biomePlaced = featurePlacer.PlaceWithBiomeCheck(
            biomeChecked,
            new LegacyRandomSource(seed),
            origin);

        Require(
            !biomePlaced && feature.Positions.Count == 0,
            "PlaceWithBiomeCheck must pass the root PlacedFeature into BiomeFilter and honor the biome's feature set.");
    }

    private static PlacementContext CreatePlacementContext(PlacedFeature? topFeature)
    {
        var world = new OverworldWorld(
            0x504C4143454D4F44L,
            LevelHeightAccessor.Create(-64, 96));
        NoiseBasedChunkGenerator generator = NoiseBasedChunkGenerator.CreateOverworld(world.Seed);
        return CreatePlacementContext(world, generator, topFeature);
    }

    private static PlacementContext CreatePlacementContext(
        OverworldWorld world,
        NoiseBasedChunkGenerator generator,
        PlacedFeature? topFeature) =>
        new(world, generator, topFeature);

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
