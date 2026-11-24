using Asteria.Core;
using Asteria.World;

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
}
