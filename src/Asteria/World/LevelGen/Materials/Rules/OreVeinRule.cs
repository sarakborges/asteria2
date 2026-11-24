using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>
/// Probabilistic ore-vein material rule from Minecraft Java 26.3.
/// Density fields are sampled from the current chunk; all per-block decisions use
/// the world's positional random factory named "ore".
/// </summary>
public sealed record OreVeinRule(
    BlockState OreBlock,
    BlockState RawOreBlock,
    BlockState FillerBlock,
    float RawOreChance,
    DensityFunction Density,
    DensityFunction Richness,
    DensityFunction FillerGap) : MaterialRule
{
    public const float VEININESS_THRESHOLD = 0.4F;
    public const int EDGE_ROUNDOFF_BEGIN = 20;
    public const float MAX_EDGE_ROUNDOFF = 0.2F;
    public const float VEIN_SOLIDNESS = 0.7F;
    public const float MIN_RICHNESS = 0.1F;
    public const float MAX_RICHNESS = 0.3F;
    public const float MAX_RICHNESS_THRESHOLD = 0.6F;
    public const float CHANCE_OF_RAW_ORE_BLOCK = 0.02F;
    public const float SKIP_ORE_IF_GAP_NOISE_IS_BELOW = -0.3F;

    private sealed record BlockParameters(
        BlockState OreBlock,
        BlockState RawOreBlock,
        BlockState FillerBlock,
        float RawOreChance)
    {
        public static MapCodec<BlockParameters> CODEC { get; } =
            RecordCodecBuilder.MapCodec<BlockParameters>(instance =>
                instance.Group(
                        BlockState.CODEC.FieldOf("ore_block")
                            .ForGetter<BlockParameters>(static value => value.OreBlock),
                        BlockState.CODEC.FieldOf("raw_ore_block")
                            .ForGetter<BlockParameters>(static value => value.RawOreBlock),
                        BlockState.CODEC.FieldOf("filler_block")
                            .ForGetter<BlockParameters>(static value => value.FillerBlock),
                        Codecs.FloatRange(0.0F, 1.0F).FieldOf("raw_ore_chance")
                            .ForGetter<BlockParameters>(static value => value.RawOreChance))
                    .Apply(instance, static (ore, rawOre, filler, chance) =>
                        new BlockParameters(ore, rawOre, filler, chance)));
    }

    private sealed record DensityParameters(
        DensityFunction Density,
        DensityFunction Richness,
        DensityFunction FillerGap)
    {
        public static MapCodec<DensityParameters> CODEC { get; } =
            RecordCodecBuilder.MapCodec<DensityParameters>(instance =>
                instance.Group(
                        DensityFunction.CODEC.FieldOf("density")
                            .ForGetter<DensityParameters>(static value => value.Density),
                        DensityFunction.CODEC.FieldOf("richness")
                            .ForGetter<DensityParameters>(static value => value.Richness),
                        DensityFunction.CODEC.FieldOf("filler_gap")
                            .ForGetter<DensityParameters>(static value => value.FillerGap))
                    .Apply(instance, static (density, richness, gap) =>
                        new DensityParameters(density, richness, gap)));
    }

    public static MapCodec<OreVeinRule> CODEC { get; } =
        RecordCodecBuilder.MapCodec<OreVeinRule>(instance =>
            instance.Group(
                    BlockParameters.CODEC.ForGetter<OreVeinRule>(
                        static rule => new BlockParameters(
                            rule.OreBlock,
                            rule.RawOreBlock,
                            rule.FillerBlock,
                            rule.RawOreChance)),
                    DensityParameters.CODEC.ForGetter<OreVeinRule>(
                        static rule => new DensityParameters(
                            rule.Density,
                            rule.Richness,
                            rule.FillerGap)))
                .Apply(instance, static (blocks, densities) =>
                    new OreVeinRule(
                        blocks.OreBlock,
                        blocks.RawOreBlock,
                        blocks.FillerBlock,
                        blocks.RawOreChance,
                        densities.Density,
                        densities.Richness,
                        densities.FillerGap)));

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(
            static rule => rule,
            static rule => (OreVeinRule)rule);

    /// <summary>Creates the vanilla copper vein rule using the supplied registered density functions.</summary>
    public static OreVeinRule CreateCopper(
        DensityFunction density,
        DensityFunction richness,
        DensityFunction fillerGap) =>
        new(
            Blocks.COPPER_ORE.DefaultState,
            Blocks.RAW_COPPER_BLOCK.DefaultState,
            Blocks.GRANITE.DefaultState,
            CHANCE_OF_RAW_ORE_BLOCK,
            density,
            richness,
            fillerGap);

    /// <summary>Creates the vanilla iron vein rule using the supplied registered density functions.</summary>
    public static OreVeinRule CreateIron(
        DensityFunction density,
        DensityFunction richness,
        DensityFunction fillerGap) =>
        new(
            Blocks.DEEPSLATE_IRON_ORE.DefaultState,
            Blocks.RAW_IRON_BLOCK.DefaultState,
            Blocks.TUFF.DefaultState,
            CHANCE_OF_RAW_ORE_BLOCK,
            density,
            richness,
            fillerGap);

    public RuleEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Func<float> densitySampler = context.GetDensitiesInChunk(Density, prefill: true);
        Func<float> richnessSampler = context.GetDensitiesInChunk(Richness, prefill: true);
        Func<float> fillerGapSampler = context.GetDensitiesInChunk(FillerGap, prefill: false);
        PositionalRandomFactory randomFactory = context.GetOrCreateRandomFactory(
            ResourceLocation.WithDefaultNamespace("ore"));

        return (blockX, blockY, blockZ) =>
        {
            float density = densitySampler();
            if (density <= 0.0F)
                return null;

            IRandomSource random = randomFactory.At(blockX, blockY, blockZ);
            if (random.NextFloat() > density)
                return null;

            float richness = richnessSampler();
            if (random.NextFloat() < richness && fillerGapSampler() < 0.0F)
                return random.NextFloat() < RawOreChance ? RawOreBlock : OreBlock;

            return FillerBlock;
        };
    }

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;
}
