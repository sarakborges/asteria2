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
        CheckConditionRuleCodecAndEvaluation();
    }

    private static void CheckConditionTypeRegistry()
    {
        Registry<MapCodec<MaterialCondition>> registry = BuiltInRegistries.MaterialConditionType;
        Require(registry.Count == 3,
            "The material-condition type registry must contain steep, not and hole.");
        Require(
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("steep")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("not")) &&
            registry.ContainsKey(ResourceLocation.WithDefaultNamespace("hole")),
            "Material-condition types must use the vanilla identifiers.");
        Require(
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("steep")), SteepCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("not")), NotCondition.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.WithDefaultNamespace("hole")), HoleCondition.TYPE_CODEC),
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
