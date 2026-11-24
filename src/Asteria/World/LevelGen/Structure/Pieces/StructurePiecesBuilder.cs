using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;

namespace Asteria.World.LevelGen.Structure.Pieces;

/// <summary>
/// Mutable builder for generated structure pieces, matching Minecraft Java 26.3.
/// </summary>
public sealed class StructurePiecesBuilder
{
    private readonly List<StructurePiece> _pieces = new();

    public void AddPiece(StructurePiece piece) =>
        _pieces.Add(piece);

    public StructurePiece? FindCollisionPiece(BoundingBox box) =>
        StructurePiece.FindCollisionPiece(_pieces, box);

    [Obsolete("Legacy vertical offset helper retained for source parity.")]
    public void OffsetPiecesVertically(int dy)
    {
        foreach (StructurePiece piece in _pieces)
            piece.Move(0, dy, 0);
    }

    [Obsolete("Legacy sea-level placement helper retained for source parity.")]
    public int MoveBelowSeaLevel(int seaLevel, int minY, IRandomSource random, int offset)
    {
        ArgumentNullException.ThrowIfNull(random);

        int maxY = unchecked(seaLevel - offset);
        BoundingBox boundingBox = GetBoundingBox();
        int y1Pos = unchecked(boundingBox.GetYSpan() + minY + 1);

        if (y1Pos < maxY)
            y1Pos = unchecked(y1Pos + random.NextInt(unchecked(maxY - y1Pos)));

        int dy = unchecked(y1Pos - boundingBox.MaxY());
        OffsetPiecesVertically(dy);
        return dy;
    }

    public void MoveInsideHeights(IRandomSource random, int lowestAllowed, int highestAllowed)
    {
        ArgumentNullException.ThrowIfNull(random);

        BoundingBox boundingBox = GetBoundingBox();
        int heightSpan = unchecked(highestAllowed - lowestAllowed + 1 - boundingBox.GetYSpan());
        int y0Pos = heightSpan > 1
            ? unchecked(lowestAllowed + random.NextInt(heightSpan))
            : lowestAllowed;

        int dy = unchecked(y0Pos - boundingBox.MinY());
        OffsetPiecesVertically(dy);
    }

    public PiecesContainer Build() =>
        new(_pieces);

    public void Clear() =>
        _pieces.Clear();

    public bool IsEmpty() =>
        _pieces.Count == 0;

    public BoundingBox GetBoundingBox() =>
        StructurePiece.CreateBoundingBox(_pieces);
}
