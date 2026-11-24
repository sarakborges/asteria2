using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>Returns the deterministic 192-block vanilla badlands clay-band material.</summary>
public sealed class BandlandsRule : MaterialRule
{
    public static BandlandsRule INSTANCE { get; } = new();

    public static MapCodec<BandlandsRule> CODEC { get; } =
        MapCodec<BandlandsRule>.Unit(INSTANCE);

    public static MapCodec<MaterialRule> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialRule>(
            static rule => rule,
            static rule => (BandlandsRule)rule);

    private BandlandsRule()
    {
    }

    public RuleEvaluator Compile(MaterialRuleContext context) =>
        (worldX, y, worldZ) => context.GetBand(worldX, y, worldZ);

    public MapCodec<MaterialRule> Codec() => TYPE_CODEC;
}
