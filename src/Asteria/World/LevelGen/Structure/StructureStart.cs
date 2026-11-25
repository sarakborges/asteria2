using System.Threading;
using Asteria.Core;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.World.LevelGen;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// A generated structure origin, its pieces, and its cross-chunk reference count.
/// This ports the in-memory StructureStart contract; NBT loading/saving and piece
/// placement are kept out until their serialization and structure-manager dependencies exist.
/// </summary>
public sealed class StructureStart
{
    public const string INVALID_START_ID = "INVALID";

    public static readonly StructureStart INVALID_START = new(
        structure: null,
        ChunkPos.Zero,
        references: 0,
        new PiecesContainer(Array.Empty<StructurePiece>()),
        allowInvalid: true);

    private readonly Structure? _structure;
    private readonly PiecesContainer _pieceContainer;
    private readonly ChunkPos _chunkPos;
    private int _references;
    private BoundingBox? _cachedBoundingBox;

    public StructureStart(
        Structure structure,
        ChunkPos chunkPos,
        int references,
        PiecesContainer pieceContainer)
        : this(
            structure ?? throw new ArgumentNullException(nameof(structure)),
            chunkPos,
            references,
            pieceContainer,
            allowInvalid: false)
    {
    }

    private StructureStart(
        Structure? structure,
        ChunkPos chunkPos,
        int references,
        PiecesContainer pieceContainer,
        bool allowInvalid = true)
    {
        if (!allowInvalid && structure is null)
            throw new ArgumentNullException(nameof(structure));

        ArgumentNullException.ThrowIfNull(chunkPos);
        ArgumentNullException.ThrowIfNull(pieceContainer);

        _structure = structure;
        _chunkPos = chunkPos;
        _references = references;
        _pieceContainer = pieceContainer;
    }

    public BoundingBox GetBoundingBox()
    {
        BoundingBox? cached = Volatile.Read(ref _cachedBoundingBox);
        if (cached is not null)
            return cached;

        Structure structure = _structure ??
            throw new InvalidOperationException(
                "The invalid structure start does not have a bounding box.");

        BoundingBox calculated = structure.AdjustBoundingBox(
            _pieceContainer.CalculateBoundingBox());

        return Interlocked.CompareExchange(
                   ref _cachedBoundingBox,
                   calculated,
                   null)
               ?? calculated;
    }

    public ChunkPos GetChunkPos() =>
        _chunkPos;

    public bool IsValid() =>
        !_pieceContainer.IsEmpty();

    public bool CanBeReferenced() =>
        _references < GetMaxReferences();

    public void AddReference() =>
        _references = unchecked(_references + 1);

    public int GetReferences() =>
        _references;

    protected int GetMaxReferences() =>
        1;

    public Structure? GetStructure() =>
        _structure;

    public IReadOnlyList<StructurePiece> GetPieces() =>
        _pieceContainer.Pieces;

    /// <summary>
    /// Dispatches this start's pieces whose bounds intersect the requested chunk box.
    /// The local port passes the services currently available in Asteria; vanilla's
    /// StructureManager/template-manager parameter remains unavailable until template
    /// loading is implemented.
    /// </summary>
    public void PlaceInChunk(
        WorldGenLevel level,
        NoiseBasedChunkGenerator generator,
        IRandomSource random,
        BoundingBox chunkBoundingBox,
        ChunkPos chunkPos)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(chunkBoundingBox);
        ArgumentNullException.ThrowIfNull(chunkPos);

        if (!IsValid() || !GetBoundingBox().Intersects(chunkBoundingBox))
            return;

        BlockPos referencePosition = chunkPos.GetMiddleBlockPosition(0);
        foreach (StructurePiece piece in _pieceContainer.Pieces)
        {
            if (piece.GetBoundingBox().Intersects(chunkBoundingBox))
            {
                piece.PostProcess(
                    level,
                    generator,
                    random,
                    chunkBoundingBox,
                    chunkPos,
                    referencePosition);
            }
        }
    }
}
