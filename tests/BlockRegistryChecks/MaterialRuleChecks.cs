using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.Util;
using System.Text.Json.Nodes;
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
        CheckBuiltInOverworldRuleRegistry();
        CheckBandlandsRuleCodec();
        CheckOreVeinRuleCodec();
        CheckBlockRuleCodecAndEvaluator();
        CheckSequenceRuleCodecAndEvaluation();
        CheckUnknownMaterialRuleType();
        CheckContextUpdates();
    }

    private static void CheckBuiltInOverworldRuleRegistry()
    {
        Registry<MaterialRule> registry = BuiltInRegistries.MaterialRule;
        Require(
            registry.Count == 37,
            "The built-in Overworld material-rule registry must contain all 37 named 26.3 rule resources.");

        ResourceLocation Key(string path) =>
            ResourceLocation.FromNamespaceAndPath("minecraft", path);

        string[] expectedKeys =
        [
            "bedrock_floor",
            "overworld",
            "overworld/biome_surface",
            "overworld/biome_surface/default",
            "overworld/copper_ore_vein",
            "overworld/iron_ore_vein",
            "overworld/surface",
            "overworld/underground",
            "overworld/under_biome_surface",
            "overworld/under_biome_surface/default"
        ];

        foreach (string path in expectedKeys)
        {
            Holder<MaterialRule>.Reference? holder = registry.GetHolder(Key(path));
            Require(
                holder is not null && holder.IsBound,
                $"Built-in material-rule resource '{path}' must resolve to a bound registry holder.");
        }

        Require(
            registry.GetValue(Key("overworld/copper_ore_vein")) is OreVeinRule copper &&
            ReferenceEquals(copper.OreBlock, Blocks.COPPER_ORE.DefaultState) &&
            registry.GetValue(Key("overworld/iron_ore_vein")) is OreVeinRule iron &&
            ReferenceEquals(iron.OreBlock, Blocks.DEEPSLATE_IRON_ORE.DefaultState),
            "Built-in Copper and Iron material-rule resources must decode to their vanilla OreVeinRule definitions.");

        Require(
            registry.GetValue(Key("overworld")) is SequenceRule
            {
                Sequence.Count: 5,
                Sequence:
                [
                    MaterialRule.HolderHolder bedrock,
                    MaterialRule.HolderHolder copperReference,
                    MaterialRule.HolderHolder ironReference,
                    ConditionRule,
                    MaterialRule.HolderHolder underground
                ]
            } &&
            ReferenceEquals(bedrock.Rule, registry.GetHolder(Key("bedrock_floor"))) &&
            ReferenceEquals(copperReference.Rule, registry.GetHolder(Key("overworld/copper_ore_vein"))) &&
            ReferenceEquals(ironReference.Rule, registry.GetHolder(Key("overworld/iron_ore_vein"))) &&
            ReferenceEquals(underground.Rule, registry.GetHolder(Key("overworld/underground"))),
            "The root Overworld sequence must retain its ordered references to bedrock, both ore veins, surface and underground rules.");
    }

    private static void CheckTypeRegistry()
    {
        Registry<MapCodec<MaterialRule>> registry = BuiltInRegistries.MaterialRuleType;
        Require(registry.Count == 5, "The material-rule type registry must contain block, bandlands, sequence, condition and ore_vein.");
        Require(
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "block")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "bandlands")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "sequence")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "condition")) &&
            registry.ContainsKey(ResourceLocation.FromNamespaceAndPath("minecraft", "ore_vein")),
            "Material-rule codec types must use the vanilla block and sequence identifiers.");
        Require(
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "block")), BlockRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "bandlands")), BandlandsRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "sequence")), SequenceRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "condition")), ConditionRule.TYPE_CODEC) &&
            ReferenceEquals(registry.GetValue(ResourceLocation.FromNamespaceAndPath("minecraft", "ore_vein")), OreVeinRule.TYPE_CODEC),
            "The registry must expose the same map-codec instances returned by each material rule.");
        Require(
            BuiltInRegistries.MaterialRule.Count == 37 &&
            BuiltInRegistries.MaterialRule.IsFrozen,
            "The built-in Overworld material-rule registry must contain all 37 decoded values and be frozen after bootstrap.");
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

    private static void CheckOreVeinRuleCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        OreVeinRule rule = OreVeinRule.CreateCopper(
            DensityFunctions.Constant(0.75F),
            DensityFunctions.Constant(0.25F),
            DensityFunctions.Constant(-0.5F));

        JsonElement encoded = MaterialRule.CODEC.EncodeStart(ops, rule).GetOrThrow();
        Require(
            ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:ore_vein" &&
            ops.Get(encoded, "ore_block").GetOrThrow().GetString() == "minecraft:copper_ore" &&
            ops.Get(encoded, "raw_ore_block").GetOrThrow().GetString() == "minecraft:raw_copper_block" &&
            ops.Get(encoded, "filler_block").GetOrThrow().GetString() == "minecraft:granite" &&
            Math.Abs(ops.Get(encoded, "raw_ore_chance").GetOrThrow().GetSingle() - 0.02F) < 0.000001F &&
            encoded.EnumerateObject().Count() == 8,
            "OreVeinRule must serialize all seven payload fields using the vanilla field names and discriminator.");

        MaterialRule decoded = MaterialRule.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded is OreVeinRule,
            "OreVeinRule discriminator must decode to the OreVeinRule implementation.");
        OreVeinRule copper = (OreVeinRule)decoded;
        Require(
            ReferenceEquals(copper.OreBlock, Blocks.COPPER_ORE.DefaultState) &&
            ReferenceEquals(copper.RawOreBlock, Blocks.RAW_COPPER_BLOCK.DefaultState) &&
            ReferenceEquals(copper.FillerBlock, Blocks.GRANITE.DefaultState),
            "OreVeinRule must resolve all three registered block-state fields to canonical states.");
        Require(
            Math.Abs(copper.RawOreChance - 0.02F) < 0.000001F,
            "OreVeinRule must preserve the raw-ore probability field.");
        Require(
            copper.Density.Range().Equals(Interval.OfExact(0.75F)),
            $"OreVeinRule density must round-trip exactly; got {copper.Density.Range()}.");
        Require(
            copper.Richness.Range().Equals(Interval.OfExact(0.25F)),
            $"OreVeinRule richness must round-trip exactly; got {copper.Richness.Range()}.");
        Require(
            copper.FillerGap.Range().Equals(Interval.OfExact(-0.5F)),
            $"OreVeinRule filler gap must round-trip exactly; got {copper.FillerGap.Range()}.");

        JsonNode invalidNode = JsonNode.Parse(encoded.GetRawText()) ??
            throw new InvalidOperationException("Could not clone OreVeinRule test payload.");
        invalidNode["raw_ore_chance"] = 1.01F;
        JsonElement invalidChance = JsonDocument.Parse(invalidNode.ToJsonString()).RootElement.Clone();
        Require(
            OreVeinRule.CODEC.Parse(ops, invalidChance).IsError,
            "OreVeinRule.CODEC must reject a raw ore chance outside [0, 1].");

        OreVeinRule iron = OreVeinRule.CreateIron(
            DensityFunctions.Constant(1.0F),
            DensityFunctions.Constant(1.0F),
            DensityFunctions.Constant(-1.0F));
        Require(
            ReferenceEquals(iron.OreBlock, Blocks.DEEPSLATE_IRON_ORE.DefaultState) &&
            ReferenceEquals(iron.RawOreBlock, Blocks.RAW_IRON_BLOCK.DefaultState) &&
            ReferenceEquals(iron.FillerBlock, Blocks.TUFF.DefaultState) &&
            iron.RawOreChance == OreVeinRule.CHANCE_OF_RAW_ORE_BLOCK,
            "The iron vein factory must bind the vanilla deepslate-iron/raw-iron/tuff states and raw-ore probability.");
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
