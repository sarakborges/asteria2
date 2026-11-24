using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Asteria.Core;
using Asteria.World.LevelGen.Structure;

namespace Asteria.World.LevelGen.Structure.Pieces;

/// <summary>
/// Immutable collection of generated structure pieces from Minecraft Java 26.3.
/// Piece serialization remains coupled to StructurePieceType and NBT registration.
/// </summary>
public sealed class PiecesContainer : IEquatable<PiecesContainer>
{
    private readonly ReadOnlyCollection<StructurePiece> _pieces;

    public PiecesContainer(IEnumerable<StructurePiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        StructurePiece[] snapshot = pieces.ToArray();
        if (Array.Exists(snapshot, static piece => piece is null))
            throw new ArgumentNullException(nameof(pieces));

        _pieces = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<StructurePiece> Pieces =>
        _pieces;

    public bool IsEmpty() =>
        _pieces.Count == 0;

    public bool IsInsidePiece(BlockPos startPos)
    {
        ArgumentNullException.ThrowIfNull(startPos);

        foreach (StructurePiece piece in _pieces)
        {
            if (piece.GetBoundingBox().IsInside(
                    startPos.GetX(),
                    startPos.GetY(),
                    startPos.GetZ()))
            {
                return true;
            }
        }

        return false;
    }

    public BoundingBox CalculateBoundingBox() =>
        StructurePiece.CreateBoundingBox(_pieces);

    public bool Equals(PiecesContainer? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null || _pieces.Count != other._pieces.Count)
            return false;

        for (int i = 0; i < _pieces.Count; i++)
        {
            if (!ReferenceEquals(_pieces[i], other._pieces[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) =>
        obj is PiecesContainer other && Equals(other);

    public override int GetHashCode()
    {
        int result = 1;
        foreach (StructurePiece piece in _pieces)
            result = unchecked(31 * result + RuntimeHelpers.GetHashCode(piece));

        return result;
    }

    public override string ToString() =>
        $"PiecesContainer[pieces=[{string.Join(", ", _pieces)}]]";
}
