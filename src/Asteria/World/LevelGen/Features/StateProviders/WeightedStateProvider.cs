using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util.Random;
using Asteria.World;

namespace Asteria.World.LevelGen.Features.StateProviders;

/// <summary>Selects a block state from a weighted distribution.</summary>
public sealed record WeightedStateProvider(
    WeightedList<BlockState> WeightedList) : BlockStateProvider
{
    public static MapCodec<WeightedStateProvider> MAP_CODEC { get; } =
        WeightedList<BlockState>
            .CreateNonEmptyCodec(BlockState.FULL_CODEC)
            .FieldOf("entries")
            .XMap<WeightedStateProvider>(
                static entries => new WeightedStateProvider(entries),
                static provider => provider.WeightedList);

    public static MapCodec<BlockStateProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<BlockStateProvider>(
            static provider => provider,
            static provider => (WeightedStateProvider)provider);

    public override BlockState GetState(BlockGetter level, IRandomSource random, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(position);
        return WeightedList.GetRandomOrThrow(random);
    }

    public override MapCodec<BlockStateProvider> Codec() => TYPE_CODEC;
}
