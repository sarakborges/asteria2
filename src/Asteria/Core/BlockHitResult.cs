namespace Asteria.Core;

/// <summary>
/// Hit against a block's geometric shape, including miss, inside and world-border metadata.
/// </summary>
public sealed class BlockHitResult : HitResult
{
    private readonly Direction _direction;
    private readonly BlockPos _blockPos;
    private readonly bool _miss;
    private readonly bool _inside;
    private readonly bool _worldBorderHit;

    public BlockHitResult(Vec3 location, Direction direction, BlockPos position, bool inside)
        : this(false, location, direction, position, inside, false)
    {
    }

    public BlockHitResult(
        Vec3 location,
        Direction direction,
        BlockPos position,
        bool inside,
        bool worldBorderHit)
        : this(false, location, direction, position, inside, worldBorderHit)
    {
    }

    private BlockHitResult(
        bool miss,
        Vec3 location,
        Direction direction,
        BlockPos position,
        bool inside,
        bool worldBorderHit)
        : base(location)
    {
        _miss = miss;
        _direction = direction ?? throw new ArgumentNullException(nameof(direction));
        _blockPos = position ?? throw new ArgumentNullException(nameof(position));
        _inside = inside;
        _worldBorderHit = worldBorderHit;
    }

    public static BlockHitResult Miss(Vec3 location, Direction direction, BlockPos position) =>
        new(true, location, direction, position, inside: false, worldBorderHit: false);

    public BlockHitResult WithDirection(Direction direction) =>
        new(_miss, Location, direction, _blockPos, _inside, _worldBorderHit);

    public BlockHitResult WithPosition(BlockPos position) =>
        new(_miss, Location, _direction, position, _inside, _worldBorderHit);

    public BlockHitResult HitBorder() =>
        new(_miss, Location, _direction, _blockPos, _inside, worldBorderHit: true);

    public BlockPos GetBlockPos() => _blockPos;

    public Direction GetDirection() => _direction;

    public override Type GetHitType() => _miss ? Type.MISS : Type.BLOCK;

    public bool IsInside() => _inside;

    public bool IsWorldBorderHit() => _worldBorderHit;
}
