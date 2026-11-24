using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World;

namespace Asteria.World.LevelGen.Features.StateProviders;

/// <summary>Returns one configured block state without consuming random values.</summary>
public sealed record SimpleStateProvider(BlockState State) : BlockStateProvider
{
    public static MapCodec<SimpleStateProvider> MAP_CODEC { get; } =
        BlockState.FULL_CODEC
            .FieldOf("state")
            .XMap<SimpleStateProvider>(
                static state => new SimpleStateProvider(state),
                static provider => provider.State);

    public static MapCodec<BlockStateProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<BlockStateProvider>(
            static provider => provider,
            static provider => (SimpleStateProvider)provider);

    public override BlockState GetState(BlockGetter level, IRandomSource random, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(position);
        return State;
    }

    public override MapCodec<BlockStateProvider> Codec() => TYPE_CODEC;
}
