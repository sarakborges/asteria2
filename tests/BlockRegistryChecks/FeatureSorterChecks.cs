using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Features;
using Asteria.World.LevelGen.Placement;
using Asteria.World.Level;

internal static class FeatureSorterChecks
{
    private sealed class TestFeature(string name) : Feature
    {
        public string Name { get; } = name;

        public MapCodec<Feature> Codec() =>
            throw new NotSupportedException("FeatureSorter checks do not serialize feature fixtures.");

        public bool Place(
            WorldGenLevel level,
            NoiseBasedChunkGenerator generator,
            IRandomSource random,
            BlockPos origin) =>
            throw new NotSupportedException("FeatureSorter checks do not execute feature fixtures.");

        public override string ToString() => Name;
    }

    private sealed record Source(
        string Name,
        IReadOnlyList<HolderSet<PlacedFeature>> FeatureSteps)
    {
        public override string ToString() => Name;
    }

    public static void Run()
    {
        CheckDependenciesAcrossBiomes();
        CheckOrderingAcrossDecorationSteps();
        CheckSharedPlacedFeatureIdentity();
        CheckCycleDiagnostics();
        CheckBiomeGenerationSettingsFeatureBuilder();
    }

    private static void CheckDependenciesAcrossBiomes()
    {
        PlacedFeature oak = CreatePlacedFeature("oak");
        PlacedFeature birch = CreatePlacedFeature("birch");
        PlacedFeature pine = CreatePlacedFeature("pine");

        Source plains = new(
            "plains",
            [Direct(oak, birch)]);
        Source forest = new(
            "forest",
            [Direct(pine, birch)]);

        IReadOnlyList<FeatureSorter.StepFeatureData> steps =
            FeatureSorter.BuildFeaturesPerStep(
                [plains, forest],
                static source => source.FeatureSteps,
                tryReducingError: true);

        Require(
            steps.Count == 1 && steps[0].Features.Count == 3,
            "FeatureSorter must merge the distinct placed features contributed by all biome sources.");

        int oakIndex = steps[0].IndexMapping(oak);
        int birchIndex = steps[0].IndexMapping(birch);
        int pineIndex = steps[0].IndexMapping(pine);

        Require(
            oakIndex >= 0 && birchIndex >= 0 && pineIndex >= 0 &&
            oakIndex < birchIndex &&
            pineIndex < birchIndex,
            "Each biome's declared adjacent feature order must become a dependency in the merged step.");
    }

    private static void CheckOrderingAcrossDecorationSteps()
    {
        PlacedFeature lake = CreatePlacedFeature("lake");
        PlacedFeature ore = CreatePlacedFeature("ore");
        Source biome = new(
            "underground",
            [
                Direct(lake),
                Direct(ore)
            ]);

        IReadOnlyList<FeatureSorter.StepFeatureData> steps =
            FeatureSorter.BuildFeaturesPerStep(
                [biome],
                static source => source.FeatureSteps,
                tryReducingError: true);

        Require(
            steps.Count == 2 &&
            steps[0].Features.Count == 1 &&
            ReferenceEquals(steps[0].Features[0], lake) &&
            steps[1].Features.Count == 1 &&
            ReferenceEquals(steps[1].Features[0], ore),
            "FeatureSorter must preserve the decoration-step boundaries when producing its ordered result.");

        Require(
            steps[0].IndexMapping(lake) == 0 &&
            steps[1].IndexMapping(ore) == 0 &&
            steps[0].IndexMapping(ore) == -1,
            "Each StepFeatureData index map must be local to that step's ordered feature list.");
    }

    private static void CheckSharedPlacedFeatureIdentity()
    {
        PlacedFeature shared = CreatePlacedFeature("shared");
        Source plains = new("plains", [Direct(shared)]);
        Source forest = new("forest", [Direct(shared)]);

        IReadOnlyList<FeatureSorter.StepFeatureData> steps =
            FeatureSorter.BuildFeaturesPerStep(
                [plains, forest],
                static source => source.FeatureSteps,
                tryReducingError: true);

        Require(
            steps.Count == 1 &&
            steps[0].Features.Count == 1 &&
            ReferenceEquals(steps[0].Features[0], shared),
            "A PlacedFeature instance shared by several biomes must receive one global feature index and one ordered entry.");
    }

    private static void CheckCycleDiagnostics()
    {
        PlacedFeature first = CreatePlacedFeature("first");
        PlacedFeature second = CreatePlacedFeature("second");
        Source plains = new("plains", [Direct(first, second)]);
        Source forest = new("forest", [Direct(second, first)]);

        bool detected = false;
        try
        {
            _ = FeatureSorter.BuildFeaturesPerStep(
                [plains, forest],
                static source => source.FeatureSteps,
                tryReducingError: true);
        }
        catch (InvalidOperationException exception)
        {
            detected =
                exception.Message.Contains("Feature order cycle found", StringComparison.Ordinal) &&
                exception.Message.Contains("plains", StringComparison.Ordinal) &&
                exception.Message.Contains("forest", StringComparison.Ordinal);
        }

        Require(
            detected,
            "Conflicting biome feature orders must fail validation and identify the sources involved in the cycle.");

        bool strictFailure = false;
        try
        {
            _ = FeatureSorter.BuildFeaturesPerStep(
                [plains, forest],
                static source => source.FeatureSteps,
                tryReducingError: false);
        }
        catch (InvalidOperationException exception)
        {
            strictFailure = exception.Message == "Feature order cycle found";
        }

        Require(
            strictFailure,
            "FeatureSorter must expose the strict cycle failure used while reducing diagnostic sources.");
    }

    private static void CheckBiomeGenerationSettingsFeatureBuilder()
    {
        PlacedFeature lake = CreatePlacedFeature("lava_lake");
        PlacedFeature topLayer = CreatePlacedFeature("freeze_top_layer");
        var builder = new BiomeGenerationSettings.PlainBuilder();
        BiomeGenerationSettings settings = builder
            .AddFeature(GenerationStep.Decoration.LAKES, Holder<PlacedFeature>.Direct(lake))
            .AddFeature(GenerationStep.Decoration.TOP_LAYER_MODIFICATION, Holder<PlacedFeature>.Direct(topLayer))
            .Build();

        Require(
            settings.Features.Count == GenerationStep.Decoration.Values.Count &&
            settings.Features[0].Count == 0 &&
            ReferenceEquals(settings.Features[1][0].Value, lake) &&
            ReferenceEquals(settings.Features[10][0].Value, topLayer),
            "BiomeGenerationSettings.PlainBuilder must allocate ordered empty groups up to the selected decoration step.");

        Require(
            settings.HasFeature(lake) &&
            settings.HasFeature(topLayer) &&
            !settings.HasFeature(CreatePlacedFeature("unlisted")),
            "BiomeGenerationSettings.HasFeature must test the exact placed-feature instance across all steps.");

        Require(
            BiomeGenerationSettings.EMPTY.Features.Count == 0 &&
            !BiomeGenerationSettings.EMPTY.Features.Any(),
            "BiomeGenerationSettings.EMPTY must retain an empty placed-feature step list.");
    }

    private static HolderSet<PlacedFeature> Direct(params PlacedFeature[] features) =>
        HolderSet<PlacedFeature>.Direct(
            features.Select(static feature => Holder<PlacedFeature>.Direct(feature)));

    private static PlacedFeature CreatePlacedFeature(string name)
    {
        Feature feature = new TestFeature(name);
        return new PlacedFeature(
            Holder<Feature>.Direct(feature),
            Array.Empty<PlacementModifier>());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
