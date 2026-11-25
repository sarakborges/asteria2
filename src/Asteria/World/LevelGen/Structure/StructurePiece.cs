using Asteria.Content.Blocks;
using Asteria.World.LevelGen;
using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.Structure.Pools;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Core structure-piece bounds and placement-depth behavior from Minecraft Java 26.3.
/// Serialization type registration and block-placement methods belong to the remaining
/// structure-piece pipeline and are intentionally not represented by placeholder APIs here.
/// </summary>
public abstract class StructurePiece
{
    protected BoundingBox _boundingBox;
    protected int _genDepth;
    private Direction? _orientation;
    private Mirror? _mirror;
    private Rotation? _rotation;

    protected StructurePiece(int genDepth, BoundingBox boundingBox)
    {
        ArgumentNullException.ThrowIfNull(boundingBox);

        _genDepth = genDepth;
        _boundingBox = boundingBox;
    }

    public BoundingBox GetBoundingBox() =>
        _boundingBox;

    public int GetGenDepth() =>
        _genDepth;

    /// <summary>
    /// Hand-built pieces contribute rigid terrain by default. Jigsaw pool pieces override
    /// this according to their StructureTemplatePool.Projection.
    /// </summary>
    public virtual bool IsRigidForTerrain => true;

    /// <summary>Vertical placement offset used by the terrain density sampler.</summary>
    public virtual int GetGroundLevelDelta() => 0;

    /// <summary>Jigsaw connector junctions contributed independently of rigid projection.</summary>
    public virtual IReadOnlyList<JigsawJunction> GetJunctions() =>
        Array.Empty<JigsawJunction>();

    /// <summary>
    /// Places this piece's portion inside one chunk box. Concrete structure-piece
    /// types must override this before they can be used for generation; the base
    /// implementation fails closed rather than silently skipping the piece.
    /// </summary>
    public virtual void PostProcess(
        WorldGenLevel level,
        NoiseBasedChunkGenerator generator,
        IRandomSource random,
        BoundingBox chunkBoundingBox,
        ChunkPos chunkPos,
        BlockPos referencePosition)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(chunkBoundingBox);
        ArgumentNullException.ThrowIfNull(chunkPos);
        ArgumentNullException.ThrowIfNull(referencePosition);

        throw new NotSupportedException(
            $"Structure piece type {GetType().FullName} has no PostProcess implementation.");
    }

    public void SetGenDepth(int genDepth) =>
        _genDepth = genDepth;

    /// <summary>
    /// Checks the piece against the chunk's horizontal block bounds expanded by distance.
    /// The vertical axis is deliberately ignored, as in StructurePiece.isCloseToChunk.
    /// </summary>
    public bool IsCloseToChunk(ChunkPos position, int distance)
    {
        int chunkMinX = position.GetMinBlockX();
        int chunkMinZ = position.GetMinBlockZ();

        return _boundingBox.Intersects(
            unchecked(chunkMinX - distance),
            unchecked(chunkMinZ - distance),
            unchecked(chunkMinX + 15 + distance),
            unchecked(chunkMinZ + 15 + distance));
    }

    public BlockPos GetLocatorPosition() =>
        _boundingBox.GetCenter();

    protected static Direction GetRandomHorizontalDirection(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        Direction[] horizontalDirections = Direction.Plane.HORIZONTAL.GetDirections();
        return horizontalDirections[random.NextInt(horizontalDirections.Length)];
    }

    protected static BoundingBox MakeBoundingBox(
        int x,
        int y,
        int z,
        Direction direction,
        int width,
        int height,
        int depth)
    {
        ArgumentNullException.ThrowIfNull(direction);

        return ReferenceEquals(direction.GetAxis(), Direction.Axis.Z)
            ? new BoundingBox(
                x, y, z,
                unchecked(x + width - 1),
                unchecked(y + height - 1),
                unchecked(z + depth - 1))
            : new BoundingBox(
                x, y, z,
                unchecked(x + depth - 1),
                unchecked(y + height - 1),
                unchecked(z + width - 1));
    }

    public Direction? GetOrientation() =>
        _orientation;

    public void SetOrientation(Direction? orientation)
    {
        _orientation = orientation;

        if (orientation is null)
        {
            _rotation = Rotation.NONE;
            _mirror = Mirror.NONE;
            return;
        }

        if (ReferenceEquals(orientation, Direction.SOUTH))
        {
            _mirror = Mirror.LEFT_RIGHT;
            _rotation = Rotation.NONE;
        }
        else if (ReferenceEquals(orientation, Direction.WEST))
        {
            _mirror = Mirror.LEFT_RIGHT;
            _rotation = Rotation.CLOCKWISE_90;
        }
        else if (ReferenceEquals(orientation, Direction.EAST))
        {
            _mirror = Mirror.NONE;
            _rotation = Rotation.CLOCKWISE_90;
        }
        else
        {
            _mirror = Mirror.NONE;
            _rotation = Rotation.NONE;
        }
    }

    public Rotation? GetRotation() =>
        _rotation;

    public Mirror? GetMirror() =>
        _mirror;

    protected BlockPos.MutableBlockPos GetWorldPos(int x, int y, int z) =>
        new(GetWorldX(x, z), GetWorldY(y), GetWorldZ(x, z));

    protected int GetWorldX(int x, int z)
    {
        if (_orientation is null)
            return x;

        if (ReferenceEquals(_orientation, Direction.NORTH) ||
            ReferenceEquals(_orientation, Direction.SOUTH))
            return unchecked(_boundingBox.MinX() + x);

        if (ReferenceEquals(_orientation, Direction.WEST))
            return unchecked(_boundingBox.MaxX() - z);

        if (ReferenceEquals(_orientation, Direction.EAST))
            return unchecked(_boundingBox.MinX() + z);

        return x;
    }

    protected int GetWorldY(int y) =>
        _orientation is null
            ? y
            : unchecked(y + _boundingBox.MinY());

    protected int GetWorldZ(int x, int z)
    {
        if (_orientation is null)
            return z;

        if (ReferenceEquals(_orientation, Direction.NORTH))
            return unchecked(_boundingBox.MaxZ() - z);

        if (ReferenceEquals(_orientation, Direction.SOUTH))
            return unchecked(_boundingBox.MinZ() + z);

        if (ReferenceEquals(_orientation, Direction.WEST) ||
            ReferenceEquals(_orientation, Direction.EAST))
            return unchecked(_boundingBox.MinZ() + x);

        return z;
    }

    public void Move(int dx, int dy, int dz) =>
        _boundingBox.Move(dx, dy, dz);

    public static StructurePiece? FindCollisionPiece(
        IReadOnlyList<StructurePiece> pieces,
        BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(box);

        foreach (StructurePiece piece in pieces)
        {
            if (piece.GetBoundingBox().Intersects(box))
                return piece;
        }

        return null;
    }

    /// <summary>
    /// Encompasses every supplied piece box. Minecraft throws when the stream has no elements.
    /// </summary>
    public static BoundingBox CreateBoundingBox(IEnumerable<StructurePiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        return BoundingBox.EncapsulatingBoxes(
                   pieces.Select(static piece => piece.GetBoundingBox()))
               ?? throw new InvalidOperationException(
                   "Unable to calculate boundingbox without pieces");
    }
}
