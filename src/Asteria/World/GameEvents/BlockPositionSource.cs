using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.GameEvents;

/// <summary>A position source fixed to the center of a block position.</summary>
public sealed record BlockPositionSource : IPositionSource
{
    public BlockPos Pos { get; }

    public static MapCodec<BlockPositionSource> CODEC { get; } =
        BlockPos.CODEC.FieldOf("pos").XMap<BlockPositionSource>(
            static pos => new BlockPositionSource(pos),
            static source => source.Pos);

    public BlockPositionSource(BlockPos pos) =>
        Pos = pos ?? throw new ArgumentNullException(nameof(pos));

    public Vec3? GetPosition() => Vec3.AtCenterOf(Pos);

    public PositionSourceType GetSourceType() => PositionSourceTypes.BLOCK;

    /// <summary>Registered block-position source type.</summary>
    public sealed class Type : PositionSourceType<BlockPositionSource>
    {
        public override MapCodec<BlockPositionSource> Codec => CODEC;
    }
}
