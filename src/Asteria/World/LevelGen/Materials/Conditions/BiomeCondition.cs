using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Matches the current biome holder against a biome holder set.</summary>
public sealed record BiomeCondition(HolderSet<Biome> Biomes) : MaterialCondition
{
    public static MapCodec<BiomeCondition> CODEC { get; } =
        RegistryCodecs.HolderSet(Registries.BIOME)
            .FieldOf("biome_is")
            .XMap<BiomeCondition>(
                static biomes => new BiomeCondition(biomes),
                static condition => condition.Biomes);

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (BiomeCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlySet<Holder<Biome>>? possibleBiomes = context.PossibleBiomes;
        if (possibleBiomes is not null)
        {
            if (!possibleBiomes.Any(Biomes.Contains))
                return static () => false;

            if (possibleBiomes.All(Biomes.Contains))
                return static () => true;
        }

        return context.LazyYCondition(() => Biomes.Contains(context.GetBiome()));
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}
