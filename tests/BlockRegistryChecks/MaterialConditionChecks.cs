using System.Text.Json;
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
        CheckConditionRuleCodecAndEvaluation();
    }

    private static void CheckConditionTypeRegistry()
    {
        Registry<MapCodec<MaterialCondition>> registry = BuiltInRegistries.MaterialConditionType;
        Require(registry.Count == 6,
            "The material-condition type registry must contain water, steep, not, hole, above_preliminary_surface and stone_depth.");
        Require(
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("steep")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("not")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("hole")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("water")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("above_preliminary_surface")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("stone_depth")),
            "Material-condition types must use the vanilla identifiers.");
        Require(
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("steep")), SteepCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("not")), NotCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("hole")), HoleCondition.TYPE_CODEC) &&
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
