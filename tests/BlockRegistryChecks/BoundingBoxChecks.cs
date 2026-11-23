using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen.Structure;

internal static class BoundingBoxChecks
{
    public static void Run()
    {
        var box = new BoundingBox(-4, 10, -8, 3, 14, 7);
        Require(box.MinX() == -4 && box.MinY() == 10 && box.MinZ() == -8 &&
            box.MaxX() == 3 && box.MaxY() == 14 && box.MaxZ() == 7,
            "BoundingBox must preserve all inclusive min/max coordinates.");
        Require(box.GetXSpan() == 8 && box.GetYSpan() == 5 && box.GetZSpan() == 16 &&
            box.GetLength().Equals(new Vec3i(7, 4, 15)),
            "BoundingBox spans and length must distinguish inclusive size from coordinate difference.");
        Require(box.IsInside(-4, 10, -8) && box.IsInside(3, 14, 7) &&
            !box.IsInside(4, 14, 7) && !box.IsInside(-4, 9, -8),
            "BoundingBox containment must include each minimum and maximum face.");

        Require(box.Intersects(new BoundingBox(3, 14, 7, 10, 20, 12)) &&
            box.Intersects(-10, 14, -2, -4, 20, 7) &&
            box.Intersects(-10, -8, 3, 7) &&
            !box.Intersects(4, 15, 8, 20, 20, 20),
            "BoundingBox intersection must be inclusive on touching faces.");

        var inverted = new BoundingBox(4, 3, 2, -4, -3, -2);
        Require(inverted.Equals(new BoundingBox(-4, -3, -2, 4, 3, 2)),
            "An inverted BoundingBox constructor must normalize each axis after diagnostics.");

        Require(BoundingBox.FromCorners(new Vec3i(2, 3, 4), new Vec3i(-1, 7, 0))
                .Equals(new BoundingBox(-1, 3, 0, 2, 7, 4)),
            "BoundingBox.fromCorners must order both corner vectors component-wise.");
        Require(BoundingBox.EncapsulatingPositions(Array.Empty<BlockPos>()) is null &&
            BoundingBox.EncapsulatingBoxes(Array.Empty<BoundingBox>()) is null,
            "BoundingBox encapsulation factories must return empty for empty inputs.");
        Require(BoundingBox.EncapsulatingPositions(new[]
            {
                new BlockPos(5, 0, 2), new BlockPos(-2, 3, 8), new BlockPos(1, -4, -1)
            })!.Equals(new BoundingBox(-2, -4, -1, 5, 3, 8)),
            "BoundingBox.encapsulatingPositions must calculate extrema for all supplied points.");
        Require(BoundingBox.EncapsulatingBoxes(new[]
            {
                new BoundingBox(0, 0, 0, 1, 1, 1),
                new BoundingBox(-5, 4, 2, 2, 8, 7)
            })!.Equals(new BoundingBox(-5, 0, 0, 2, 8, 7)),
            "BoundingBox.encapsulatingBoxes must calculate extrema for all supplied boxes.");

        BoundingBox moved = box.Moved(10, -2, 3);
        Require(moved.Equals(new BoundingBox(6, 8, -5, 13, 12, 10)) &&
            box.Equals(new BoundingBox(-4, 10, -8, 3, 14, 7)),
            "BoundingBox.moved must return a translated copy without mutating the source.");
        Require(ReferenceEquals(box.Move(1, 2, 3), box) &&
            box.Equals(new BoundingBox(-3, 12, -5, 4, 16, 10)),
            "BoundingBox.move must translate the mutable box in place and return the same instance.");
        Require(box.InflatedBy(2, 1, 3).Equals(new BoundingBox(-5, 11, -8, 6, 17, 13)),
            "BoundingBox.inflatedBy must expand each axis by its own amount.");
        Require(box.GetCenter().Equals(new BlockPos(1, 14, 3)),
            "BoundingBox.getCenter must preserve the reference integer-center formula.");

        var corners = new List<string>();
        box.ForAllCorners(position => corners.Add(position.ToShortString()));
        Require(corners.SequenceEqual(new[]
            {
                "4, 16, 10", "-3, 16, 10", "4, 12, 10", "-3, 12, 10",
                "4, 16, -5", "-3, 16, -5", "4, 12, -5", "-3, 12, -5"
            }),
            "BoundingBox.forAllCorners must visit the eight corners in the reference order.");

        Require(BoundingBox.Encapsulating(box, moved).Equals(new BoundingBox(-3, 8, -5, 13, 16, 10)),
            "BoundingBox.encapsulating must create a separate union of the two input boxes.");
        Require(ReferenceEquals(box.Encapsulate(new BlockPos(100, 0, 0)), box) &&
            box.MaxX() == 100 && box.MinY() == 0,
            "BoundingBox.encapsulate(BlockPos) must mutate and return the receiver.");

        Require(BoundingBox.OrientBox(10, 20, 30, 1, 2, 3, 4, 5, 6, Direction.SOUTH)
                .Equals(new BoundingBox(11, 22, 33, 14, 26, 38)) &&
            BoundingBox.OrientBox(10, 20, 30, 1, 2, 3, 4, 5, 6, Direction.NORTH)
                .Equals(new BoundingBox(11, 22, 28, 14, 26, 33)) &&
            BoundingBox.OrientBox(10, 20, 30, 1, 2, 3, 4, 5, 6, Direction.WEST)
                .Equals(new BoundingBox(8, 22, 31, 13, 26, 34)) &&
            BoundingBox.OrientBox(10, 20, 30, 1, 2, 3, 4, 5, 6, Direction.EAST)
                .Equals(new BoundingBox(13, 22, 31, 18, 26, 34)),
            "BoundingBox.orientBox must preserve the reference directional placement formulas.");

        Require(box.IntersectingChunks().SequenceEqual(new[]
            {
                new ChunkPos(-1, -1), new ChunkPos(0, -1),
                new ChunkPos(-1, 0), new ChunkPos(0, 0)
            }),
            "BoundingBox.intersectingChunks must enumerate inclusive intersecting chunk coordinates.");
        Require(BoundingBox.Infinite().Equals(new BoundingBox(
                int.MinValue, int.MinValue, int.MinValue,
                int.MaxValue, int.MaxValue, int.MaxValue)),
            "BoundingBox.infinite must use the full signed integer coordinate range.");
        Require(box.ToString() == "BoundingBox{minX=-3, minY=0, minZ=-5, maxX=100, maxY=16, maxZ=10}",
            "BoundingBox.toString must preserve field names and their declaration order.");
        Require(box.GetHashCode() == new BoundingBox(-3, 0, -5, 100, 16, 10).GetHashCode(),
            "Equal bounding boxes must have identical Java-compatible hash codes.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
