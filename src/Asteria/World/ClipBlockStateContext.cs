using Asteria.Content.Blocks;
using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Defines a block-state predicate along a line segment for BlockGetter line queries.
/// </summary>
public sealed class ClipBlockStateContext
{
    private readonly Predicate<BlockState> _targetBlock;

    public Vec3 From { get; }

    public Vec3 To { get; }

    public ClipBlockStateContext(
        Vec3 from,
        Vec3 to,
        Predicate<BlockState> targetBlock)
    {
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        _targetBlock = targetBlock ?? throw new ArgumentNullException(nameof(targetBlock));
    }

    public Vec3 GetFrom() => From;

    public Vec3 GetTo() => To;

    public bool IsTargetBlock(BlockState state) =>
        _targetBlock(state ?? throw new ArgumentNullException(nameof(state)));
}
