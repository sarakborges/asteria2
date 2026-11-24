using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen.Features.StateProviders;

namespace Asteria.World.LevelGen.Features;

/// <summary>
/// Places one state selected by a BlockStateProvider, after the state's survival check.
/// The DoublePlantBlock and MossyCarpetBlock special placement paths are not yet
/// reachable because those block families have not been ported into Asteria.
/// </summary>
public sealed record SimpleBlockFeature(
    Holder<BlockStateProvider> ToPlace,
    bool ScheduleTick) : Feature
{
    public static MapCodec<SimpleBlockFeature> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<SimpleBlockFeature>(instance =>
            instance.Group(
                    BlockStateProvider.CODEC
                        .FieldOf("to_place")
                        .ForGetter<SimpleBlockFeature>(static feature => feature.ToPlace),
                    Codecs.BOOL
                        .OptionalFieldOf("schedule_tick", false)
                        .ForGetter<SimpleBlockFeature>(static feature => feature.ScheduleTick))
                .Apply(
                    instance,
                    static (toPlace, scheduleTick) =>
                        new SimpleBlockFeature(toPlace, scheduleTick)));

    public static MapCodec<Feature> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<Feature>(
            static feature => feature,
            static feature => (SimpleBlockFeature)feature);

    public SimpleBlockFeature(Holder<BlockStateProvider> toPlace)
        : this(toPlace, false)
    {
    }

    public SimpleBlockFeature(BlockStateProvider toPlace)
        : this(Holder<BlockStateProvider>.Direct(
            toPlace ?? throw new ArgumentNullException(nameof(toPlace))))
    {
    }

    public MapCodec<Feature> Codec() => TYPE_CODEC;

    public bool Place(
        WorldGenLevel level,
        NoiseBasedChunkGenerator generator,
        IRandomSource random,
        BlockPos origin)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(origin);

        BlockState? stateToPlace = ToPlace.Value.GetOptionalState(level, random, origin);
        if (stateToPlace is null || !stateToPlace.CanSurvive(level, origin))
            return false;

        _ = level.SetBlock(origin, stateToPlace, updateFlags: 2);
        if (ScheduleTick)
            level.ScheduleBlockTick(origin, stateToPlace.Block, delay: 1);

        return true;
    }
}
