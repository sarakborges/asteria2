using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Materials.Rules;

internal static class MaterialRuleChecks
{
    private sealed record NullRule : MaterialRule
    {
        private static readonly MapCodec<MaterialRule> CODEC =
            MapCodec<NullRule>.Unit(static () => new NullRule())
                .XMap<MaterialRule>(static rule => rule, static rule => (NullRule)rule);

        public RuleEvaluator Compile(MaterialRuleContext context) =>
            static (_, _, _) => null;

        public MapCodec<MaterialRule> Codec() => CODEC;
    }

    public static void Run()
    {
        CheckTypeRegistry();
        CheckBandlandsRuleCodec();
        CheckBlockRuleCodecAndEvaluator();
        CheckSequenceRuleCodecAndEvaluation();
        CheckUnknownMaterialRuleType();
        CheckContextUpdates();
    }

    private static void CheckTypeRegistry()
    {
        Registry<MapCodec<MaterialRule>> registry = BuiltInRegistries.MaterialRuleType;
        Require(registry.Count == 4, "The material-rule type registry must contain block, bandlands, sequence and condition.");
        Require(
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "block")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "bandlands")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "sequence")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "condition")),
            "Material-rule codec types must use the vanilla block and sequence identifiers.");
        Require(
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "block")), BlockRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "bandlands")), BandlandsRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "sequence")), SequenceRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "condition")), ConditionRule.TYPE_CODEC),
            "The registry must expose the same map-codec instances returned by each material rule.");
        Require(
            BuiltInRegistries.MaterialRule.Count == 0,
            "The material-rule value registry must remain empty until data-driven material rules are bootstrapped.");
    }

    private static void CheckBandlandsRuleCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement encoded = MaterialRule.CODEC.EncodeStart(ops, BandlandsRule.INSTANCE).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:bandlands" &&
            encoded.EnumerateObject().Count() == 1,
            "BandlandsRule must serialize as the vanilla unit rule with no payload fields.");

        MaterialRule decoded = MaterialRule.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            ReferenceEquals(decoded, BandlandsRule.INSTANCE),
            "BandlandsRule must decode to its singleton instance.");
    }

    private static void CheckBlockRuleCodecAndEvaluator()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialRule rule = new BlockRule(Blocks.STONE.DefaultState);
        JsonElement encoded = MaterialRule.CODEC.EncodeStart(ops, rule).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:block" &&
            ops.Get(encoded, "result_state").GetOrThrow().GetString() == "minecraft:stone",
            "BlockRule must serialize with the vanilla block discriminator and result_state field.");

        MaterialRule decoded = MaterialRule.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is BlockRule block && ReferenceEquals(block.ResultState, Blocks.STONE.DefaultState),
            "BlockRule must round-trip through MaterialRule.CODEC using the registered block state.");

        RuleEvaluator evaluator = decoded.Compile(null!);
        Require(
            ReferenceEquals(evaluator(12, -24, 9), Blocks.STONE.DefaultState),
            "A compiled BlockRule must always return its configured state.");
    }

    private static void CheckSequenceRuleCodecAndEvaluation()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        MaterialRule rule = new SequenceRule(
        [
            new BlockRule(Blocks.DIRT.DefaultState),
            new BlockRule(Blocks.STONE.DefaultState)
        ]);
        JsonElement encoded = MaterialRule.CODEC.EncodeStart(ops, rule).GetOrThrow();

        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:sequence" &&
            ops.Get(encoded, "sequence").GetOrThrow().GetArrayLength() == 2,
            "SequenceRule must serialize its ordered children under the vanilla sequence field.");

        MaterialRule decoded = MaterialRule.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded is SequenceRule sequence && sequence.Sequence.Count == 2,
            "SequenceRule must preserve every child when round-tripped.");

        RuleEvaluator evaluator = new SequenceRule(
        [
            new NullRule(),
            new BlockRule(Blocks.DIRT.DefaultState),
            new BlockRule(Blocks.STONE.DefaultState)
        ]).Compile(null!);
        Require(
            ReferenceEquals(evaluator(-4, 20, 8), Blocks.DIRT.DefaultState),
            "SequenceRule must return the first non-null state.");

        RuleEvaluator single = new SequenceRule([new BlockRule(Blocks.GRASS_BLOCK.DefaultState)])
            .Compile(null!);
        Require(
            ReferenceEquals(single(1, 2, 3), Blocks.GRASS_BLOCK.DefaultState),
            "A one-entry SequenceRule must use the single-child evaluator path.");

        RuleEvaluator empty = new SequenceRule(Array.Empty<MaterialRule>()).Compile(null!);
        Require(empty(1, 2, 3) is null,
            "An empty SequenceRule evaluator must return null, matching the vanilla loop semantics.");
    }

    private static void CheckUnknownMaterialRuleType()
    {
        JsonElement invalid = JsonDocument.Parse("""
            { "type": "not_registered", "result_state": "minecraft:stone" }
            """).RootElement.Clone();

        Require(
            MaterialRule.CODEC.Parse(JsonOps.INSTANCE, invalid).IsError,
            "MaterialRule.CODEC must reject an unknown material-rule type identifier.");
    }

    private static void CheckContextUpdates()
    {
        var context = new MaterialRuleContext();
        context.UpdateXZ(10, -12, 2, -3);
        Require(
            context.LastUpdateXZ == long.MinValue + 2 &&
            context.LastUpdateY == long.MinValue + 2 &&
            context.BlockX == 10 &&
            context.BlockZ == -12 &&
            context.SurfaceGradientX == 2 &&
            context.SurfaceGradientZ == -3,
            "MaterialRuleContext.UpdateXZ must update column coordinates and invalidate Y-scoped caches.");

        context.UpdateY(4, 5, 63, -31);
        Require(
            context.LastUpdateY == long.MinValue + 3 &&
            context.BlockY == -31 &&
            context.StoneDepthAbove == 4 &&
            context.StoneDepthBelow == 5 &&
            context.WaterHeight == 63,
            "MaterialRuleContext.UpdateY must update block coordinates and invalidate Y-scoped caches.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
