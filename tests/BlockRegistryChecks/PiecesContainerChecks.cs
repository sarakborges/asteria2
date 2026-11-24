using System.Collections.Generic;
using Asteria.Core;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class PiecesContainerChecks
{
    public static void Run()
    {
        CheckSnapshotAndEquality();
        CheckContainment();
        CheckBoundingBox();
    }

    private static void CheckSnapshotAndEquality()
    {
        var first = CreatePiece(new BoundingBox(0, 0, 0, 2, 2, 2));
        var second = CreatePiece(new BoundingBox(10, 10, 10, 12, 12, 12));
        var source = new List<StructurePiece> { first };
        var container = new PiecesContainer(source);

        source.Add(second);

        Require(container.Pieces.Count == 1 && ReferenceEquals(container.Pieces[0], first),
            "PiecesContainer must snapshot the supplied list rather than retain a mutable input list.");
        Require(container.IsEmpty() == false,
            "PiecesContainer.isEmpty must reflect its copied piece list.");
        Require(container.Equals(new PiecesContainer(new[] { first })) &&
                container.GetHashCode() == new PiecesContainer(new[] { first }).GetHashCode(),
            "PiecesContainer record equality must compare piece lists by their contents.");

        bool rejectedMutation = false;
        try
        {
            ((IList<StructurePiece>)container.Pieces).Add(second);
        }
        catch (NotSupportedException)
        {
            rejectedMutation = true;
        }

        Require(rejectedMutation,
            "PiecesContainer.pieces must expose an immutable list, matching List.copyOf.");
    }

    private static void CheckContainment()
    {
        var first = CreatePiece(new BoundingBox(-5, 10, -7, 4, 20, 8));
        var second = CreatePiece(new BoundingBox(100, 0, -20, 100, 0, -20));
        var container = new PiecesContainer(new[] { first, second });

        Require(container.IsInsidePiece(new BlockPos(-5, 10, -7)) &&
                container.IsInsidePiece(new BlockPos(4, 20, 8)) &&
                container.IsInsidePiece(new BlockPos(100, 0, -20)),
            "PiecesContainer.isInsidePiece must include inclusive box boundaries for any piece.");
        Require(!container.IsInsidePiece(new BlockPos(5, 20, 8)) &&
                !container.IsInsidePiece(new BlockPos(0, 9, 0)),
            "PiecesContainer.isInsidePiece must reject positions outside every piece box.");

        Require(new PiecesContainer(Array.Empty<StructurePiece>())
                    .IsInsidePiece(new BlockPos(0, 0, 0)) == false,
            "An empty PiecesContainer must not contain any position.");
    }

    private static void CheckBoundingBox()
    {
        var first = CreatePiece(new BoundingBox(-5, 10, -7, 4, 20, 8));
        var second = CreatePiece(new BoundingBox(100, 0, -20, 100, 0, -20));
        var container = new PiecesContainer(new[] { first, second });

        Require(container.CalculateBoundingBox().Equals(
                new BoundingBox(-5, 0, -20, 100, 20, 8)),
            "PiecesContainer.calculateBoundingBox must encompass every structure piece.");

        var empty = new PiecesContainer(Array.Empty<StructurePiece>());
        Require(empty.IsEmpty(), "An empty PiecesContainer must report isEmpty.");
        RequireThrows(
            () => empty.CalculateBoundingBox(),
            "Unable to calculate boundingbox without pieces");

        RequireThrows(
            () => StructurePiece.CreateBoundingBox(Array.Empty<StructurePiece>()),
            "Unable to calculate boundingbox without pieces");
    }

    private static TestPiece CreatePiece(BoundingBox bounds) =>
        new(bounds);

    private sealed class TestPiece(BoundingBox bounds) : StructurePiece(0, bounds);

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message == message,
                "Structure-piece bounds aggregation must preserve the reference empty-stream exception.");
            return;
        }

        throw new InvalidOperationException(
            "Expected an InvalidOperationException from empty structure-piece bounds.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
