using System.Text.Json;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen;
using Asteria.World;
using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Materials.Conditions;
using Asteria.World.LevelGen.Materials.Rules;

internal static class MaterialConditionChecks
{
    public static void Run()
    {
        CheckConditionTypeRegistry();
        CheckNotConditionCodecAndEvaluation();
        CheckLazyXZConditions();
        CheckWaterConditionCodecAndEvaluation();
        CheckAbovePreliminarySurfaceCondition();
        CheckStoneDepthConditionCodecAndEvaluation();
        CheckYConditionCodecAndEvaluation();
        CheckVerticalGradientConditionCodecAndEvaluation();
        CheckVerticalAnchorsAndWorldGenerationContext();
        CheckConditionRuleCodecAndEvaluation();
    }

    private static void CheckConditionTypeRegistry()
    {
        Registry<MapCodec<MaterialCondition>> registry = BuiltInRegistries.MaterialConditionType;
        Require(registry.Count == 11,
            "The material-condition type registry must contain biome, noise_threshold, vertical_gradient, y_above, water, temperature, steep, not, hole, above_preliminary_surface and stone_depth.");
        Require(
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("biome")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("noise_threshold")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("temperature")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("steep")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("not")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("hole")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("vertical_gradient")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("y_above")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("water")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("above_preliminary_surface")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("stone_depth")),
            "Material-condition types must use the vanilla identifiers.");
        Require(
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("biome")), BiomeCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("noise_threshold")), NoiseThresholdCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("temperature")), TemperatureCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("steep")), SteepCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("not")), NotCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("hole")), HoleCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("vertical_gradient")), VerticalGradientCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("y_above")), YCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("water")), WaterCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("above_preliminary_surface")), AbovePreliminarySurfaceCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("stone_depth")), StoneDepthCondition.TYPE_CODEC),
            "The material-condition registry must expose the singleton or record codec instances.");
        Require(BuiltInRegistries.MaterialCondition.Count == 0,
            "The material-condition value registry stays empty until data bootstrap registers values.");
    }

    private static void CheckNotConditionCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = new NotCondition(SteepCondition.INSTANCE);
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:not" &&
            ops.Get(ops.Get(encoded, "invert").GetOrThrow(), "type").GetOrThrow().GetString() == "asteria:steep",
            "NotCondition must serialize its nested predicate through the vanilla invert field.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is NotCondition notCondition && notCondition.Target is SteepCondition,
            "NotCondition must round-trip while retaining its nested condition type.");

        var context = new MaterialRuleContext();
        context.UpdateXZ(0, 0, -4, 0, surfaceDepth: 1);
        ConditionEvaluator evaluator = decoded.Compile(context);
        Require(!evaluator(), "NotCondition must invert a true steep predicate.");
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 1);
        Require(evaluator(), "NotCondition must observe the next XZ cache generation.");
    }

    private static void CheckLazyXZConditions()
    {
        var context = new MaterialRuleContext();
        ConditionEvaluator steep = SteepCondition.INSTANCE.Compile(context);
        ConditionEvaluator hole = HoleCondition.INSTANCE.Compile(context);

        context.UpdateXZ(4, 8, -4, 0, surfaceDepth: 3);
        Require(steep() && !hole(),
            "SteepCondition must use X-gradient <= -4 and HoleCondition must reject positive surface depth.");
        context.UpdateXZ(5, 8, 0, 4, surfaceDepth: 0);
        Require(steep() && hole(),
            "Lazy XZ conditions must recompute after a column update.");
        context.UpdateXZ(5, 8, 0, 3, surfaceDepth: 1);
        Require(!steep() && !hole(),
            "Lazy XZ conditions must invalidate cached true values after a column update.");
    }

    private static void CheckWaterConditionCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = new WaterCondition(-2, 1, true);
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:water" &&
            ops.Get(encoded, "offset").GetOrThrow().GetInt32() == -2 &&
            ops.Get(encoded, "surface_depth_multiplier").GetOrThrow().GetInt32() == 1 &&
            ops.Get(encoded, "add_stone_depth").GetOrThrow().GetBoolean(),
            "WaterCondition must preserve its vanilla discriminator and codec fields.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is WaterCondition water && water.Offset == -2 &&
                water.SurfaceDepthMultiplier == 1 && water.AddStoneDepth,
            "WaterCondition must round-trip all fields.");

        var context = new MaterialRuleContext();
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 2);
        context.UpdateY(3, 8, 10, 4);
        ConditionEvaluator evaluator = decoded.Compile(context);
        Require(!evaluator(),
            "WaterCondition must include stone depth, offset and scaled surface depth.");

        context.UpdateY(3, 8, 10, 8);
        Require(evaluator(),
            "WaterCondition must invalidate its Y cache on block-height updates.");
        context.UpdateY(0, 0, int.MinValue, -100);
        Require(evaluator(),
            "WaterCondition must always pass when there is no water above the column.");

        JsonElement outOfRange = JsonDocument.Parse("""
            { "type": "asteria:water", "offset": 0, "surface_depth_multiplier": 21, "add_stone_depth": false }
            """).RootElement.Clone();
        Require(MaterialCondition.CODEC.Parse(ops, outOfRange).IsError,
            "WaterCondition must reject surface_depth_multiplier values outside [-20, 20].");
    }

    private static void CheckAbovePreliminarySurfaceCondition()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = AbovePreliminarySurfaceCondition.INSTANCE;
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:above_preliminary_surface",
            "AbovePreliminarySurfaceCondition must use the vanilla discriminator.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is AbovePreliminarySurfaceCondition,
            "AbovePreliminarySurfaceCondition must round-trip as a singleton.");

        var context = new MaterialRuleContext();
        context.UpdateXZ(0, 0, 0, 0, minSurfaceLevel: 10);
        ConditionEvaluator evaluator = decoded.Compile(context);
        context.UpdateY(0, 0, int.MinValue, 9);
        Require(!evaluator(), "AbovePreliminarySurfaceCondition must reject Y below the preliminary surface.");
        context.UpdateY(0, 0, int.MinValue, 10);
        Require(evaluator(), "AbovePreliminarySurfaceCondition must include the preliminary surface boundary.");
    }

    private static void CheckStoneDepthConditionCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = new StoneDepthCondition(
            0, true, 4, Asteria.World.LevelGen.Placement.CaveSurface.FLOOR);
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:stone_depth" &&
            ops.Get(encoded, "offset").GetOrThrow().GetInt32() == 0 &&
            ops.Get(encoded, "add_surface_depth").GetOrThrow().GetBoolean() &&
            ops.Get(encoded, "secondary_depth_range").GetOrThrow().GetInt32() == 4 &&
            ops.Get(encoded, "surface_type").GetOrThrow().GetString() == "floor",
            "StoneDepthCondition must serialize its four vanilla fields.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is StoneDepthCondition stoneDepth &&
                ReferenceEquals(stoneDepth.SurfaceType, Asteria.World.LevelGen.Placement.CaveSurface.FLOOR),
            "StoneDepthCondition must round-trip the CaveSurface value.");

        var context = new MaterialRuleContext();
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 2, surfaceSecondary: 1.0);
        context.UpdateY(3, 8, int.MinValue, 4);
        ConditionEvaluator floor = decoded.Compile(context);
        ConditionEvaluator ceiling = new StoneDepthCondition(
            0, true, 4, Asteria.World.LevelGen.Placement.CaveSurface.CEILING).Compile(context);
        Require(floor() && !ceiling(),
            "StoneDepthCondition must select stoneDepthAbove for floor and stoneDepthBelow for ceiling.");

        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 0, surfaceSecondary: -1.0);
        context.UpdateY(3, 1, int.MinValue, 4);
        Require(!floor() && ceiling(),
            "StoneDepthCondition must recompute from updated XZ/Y context generations.");

        JsonElement floorJson = JsonDocument.Parse("\"floor\"").RootElement.Clone();
        Require(
            Asteria.World.LevelGen.Placement.CaveSurface.CODEC
                .EncodeStart(ops, Asteria.World.LevelGen.Placement.CaveSurface.CEILING)
                .GetOrThrow().GetString() == "ceiling" &&
            Asteria.World.LevelGen.Placement.CaveSurface.CODEC
                .Parse(ops, floorJson).GetOrThrow() ==
                    Asteria.World.LevelGen.Placement.CaveSurface.FLOOR,
            "CaveSurface codec must use the vanilla ceiling/floor names.");
    }

    private static void CheckVerticalAnchorsAndWorldGenerationContext()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var generation = new WorldGenerationContext(-64, 384, 63);

        VerticalAnchor[] anchors =
        [
            VerticalAnchor.Absolute(-12),
            VerticalAnchor.AboveBottom(7),
            VerticalAnchor.BelowTop(5),
            VerticalAnchor.RelativeToSeaLevel(-2)
        ];
        int[] expected = [-12, -57, 314, 61];

        for (int index = 0; index < anchors.Length; index++)
        {
            JsonElement encoded = VerticalAnchor.CODEC.EncodeStart(ops, anchors[index]).GetOrThrow();
            VerticalAnchor decoded = VerticalAnchor.CODEC.Parse(ops, encoded).GetOrThrow();
            Require(
                decoded == anchors[index] &&
                decoded.ResolveY(generation) == expected[index],
                "VerticalAnchor must round-trip and resolve absolute, bottom-, top- and sea-relative values.");
        }

        Require(
            VerticalAnchor.CODEC.Parse(
                ops,
                JsonDocument.Parse("""{ "absolute": 1, "below_top": 2 }""").RootElement).IsError,
            "VerticalAnchor must reject maps with more than one anchor representation.");
        Require(
            VerticalAnchor.CODEC.Parse(
                ops,
                JsonDocument.Parse("""{ "absolute": 2032 }""").RootElement).IsError,
            "VerticalAnchor absolute values must obey the dimension Y bounds.");

        var settings = CreateNoiseSettings();
        var randomState = CreateRandomState(settings);
        var generator = new NoiseBasedChunkGenerator(settings, randomState);
        var accessor = LevelHeightAccessor.Create(-32, 64);
        var fromGenerator = new WorldGenerationContext(generator, accessor);

        Require(
            fromGenerator.GetMinGenY() == -32 &&
            fromGenerator.GetGenDepth() == 64 &&
            fromGenerator.SeaLevel() == settings.SeaLevel,
            "WorldGenerationContext must intersect generator and level height bounds while preserving sea level.");
    }

    private static void CheckYConditionCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = new YCondition(
            VerticalAnchor.Absolute(10),
            SurfaceDepthMultiplier: 2,
            AddStoneDepth: false);
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:y_above" &&
            ops.Get(encoded, "anchor").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "surface_depth_multiplier").GetOrThrow().GetInt32() == 2 &&
            !ops.Get(encoded, "add_stone_depth").GetOrThrow().GetBoolean(),
            "YCondition must serialize its vanilla y_above discriminator and fields.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is YCondition yCondition &&
                yCondition.Anchor == VerticalAnchor.Absolute(10) &&
                yCondition.SurfaceDepthMultiplier == 2 &&
                !yCondition.AddStoneDepth,
            "YCondition must round-trip its anchor and surface-depth parameters.");

        var context = new MaterialRuleContext(new WorldGenerationContext(-64, 384, 63));
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 3);
        ConditionEvaluator evaluator = decoded.Compile(context);

        context.UpdateY(5, 0, int.MinValue, 15);
        Require(!evaluator(),
            "YCondition must include surfaceDepthMultiplier when testing the current block Y.");
        context.UpdateY(5, 0, int.MinValue, 16);
        Require(evaluator(),
            "YCondition must pass at the inclusive anchor plus scaled surface depth.");

        MaterialCondition addStone = new YCondition(
            VerticalAnchor.Absolute(10),
            SurfaceDepthMultiplier: 0,
            AddStoneDepth: true);
        ConditionEvaluator stoneEvaluator = addStone.Compile(context);
        context.UpdateY(2, 0, int.MinValue, 8);
        Require(stoneEvaluator(),
            "YCondition must add stoneDepthAbove when add_stone_depth is true.");
    }

    private static void CheckVerticalGradientConditionCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialCondition condition = new VerticalGradientCondition(
            ResourceLocation.Parse("asteria:test_gradient"),
            VerticalAnchor.Absolute(0),
            VerticalAnchor.Absolute(10));
        JsonElement encoded = MaterialCondition.CODEC.EncodeStart(ops, condition).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:vertical_gradient" &&
            ops.Get(encoded, "random_name").GetOrThrow().GetString() == "asteria:test_gradient" &&
            ops.Get(encoded, "true_at_and_below").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "false_at_and_above").GetOrThrow().ValueKind == JsonValueKind.Object,
            "VerticalGradientCondition must serialize the random name and both vertical anchors.");

        MaterialCondition decoded = MaterialCondition.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is VerticalGradientCondition gradient &&
                gradient.RandomName == ResourceLocation.Parse("asteria:test_gradient"),
            "VerticalGradientCondition must round-trip its named positional random source.");

        NoiseGeneratorSettings settings = CreateNoiseSettings();
        RandomState randomState = CreateRandomState(settings);
        var generation = new WorldGenerationContext(-64, 384, 63);
        var firstContext = new MaterialRuleContext(generation, randomState);
        var secondContext = new MaterialRuleContext(generation, randomState);
        firstContext.UpdateXZ(7, -3, 0, 0);
        secondContext.UpdateXZ(7, -3, 0, 0);
        ConditionEvaluator first = decoded.Compile(firstContext);
        ConditionEvaluator second = decoded.Compile(secondContext);

        firstContext.UpdateY(0, 0, int.MinValue, 0);
        secondContext.UpdateY(0, 0, int.MinValue, 0);
        Require(first() && second(),
            "VerticalGradientCondition must be true at and below its lower anchor.");

        firstContext.UpdateY(0, 0, int.MinValue, 10);
        secondContext.UpdateY(0, 0, int.MinValue, 10);
        Require(!first() && !second(),
            "VerticalGradientCondition must be false at and above its upper anchor.");

        firstContext.UpdateY(0, 0, int.MinValue, 5);
        secondContext.UpdateY(0, 0, int.MinValue, 5);
        Require(first() == second(),
            "VerticalGradientCondition must use deterministic positional randomness for intermediate Y values.");
    }

    private static NoiseGeneratorSettings CreateNoiseSettings()
    {
        var density = new ConstantFunction(0.0f);
        var router = new NoiseRouter(
            density, density, density, density,
            density, density, density, density);
        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, 384),
            Blocks.STONE.DefaultState,
            Blocks.WATER.DefaultState,
            router,
            Holder<MaterialRule>.Direct(new BlockRule(Blocks.STONE.DefaultState)),
            Array.Empty<SpawnTargetPoint>(),
            63,
            Aquifers: null,
            UseLegacyRandomSource: false,
            DebugFunctions: NoiseGeneratorDebugFunctions.EMPTY);
    }

    private static RandomState CreateRandomState(NoiseGeneratorSettings settings) =>
        RandomState.Create(
            BuiltInRegistries.Noise,
            783124L,
            settings.UseLegacyRandomSource,
            settings.NoiseRouter);

    private static void CheckConditionRuleCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialRule rule = new ConditionRule(
            new NotCondition(HoleCondition.INSTANCE),
            new BlockRule(Blocks.STONE.DefaultState));
        JsonElement encoded = MaterialRule.CODEC.EncodeStart(ops, rule).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:condition" &&
            ops.Get(encoded, "if_true").GetOrThrow().ValueKind == JsonValueKind.Object &&
            ops.Get(encoded, "then_run").GetOrThrow().ValueKind == JsonValueKind.Object,
            "ConditionRule must serialize its vanilla if_true and then_run fields.");

        MaterialRule decoded = MaterialRule.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is ConditionRule,
            "ConditionRule must be registered in the material-rule codec dispatch.");

        var context = new MaterialRuleContext();
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 1);
        RuleEvaluator evaluator = decoded.Compile(context);
        Require(ReferenceEquals(evaluator(2, -10, 3), Blocks.STONE.DefaultState),
            "ConditionRule must apply then_run when its condition is true.");
        context.UpdateXZ(0, 0, 0, 0, surfaceDepth: 0);
        Require(evaluator(2, -10, 3) is null,
            "ConditionRule must skip then_run when its condition is false.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
