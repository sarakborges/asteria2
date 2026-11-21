using Asteria.Core;
using Asteria.Core.Shapes;

internal static class DiscreteVoxelShapeChecks
{
    public static void Run()
    {
        var zero = new BitSetDiscreteVoxelShape(0, 0, 0);
        Require(zero.IsEmpty(), "Zero-sized discrete shapes must be empty.");
        Require(zero.GetSize(Direction.Axis.X) == 0, "Axis dimensions must be addressable.");
        RequireThrows<ArgumentException>(() => _ = new BitSetDiscreteVoxelShape(-1, 2, 3),
            "Negative dimensions must be rejected.");

        var shape = new BitSetDiscreteVoxelShape(4, 3, 5);
        Require(shape.IsEmpty(), "A new shape must be empty.");
        Require(!shape.IsFullWide(-1, 0, 0) && !shape.IsFullWide(4, 0, 0),
            "IsFullWide must reject coordinates outside the discrete volume.");
        shape.Fill(2, 1, 3);
        shape.Fill(0, 2, 1);
        shape.Fill(3, 0, 4);
        shape.Fill(2, 1, 3);
        Require(!shape.IsEmpty(), "Filling cells must make the shape non-empty.");
        Require(shape.IsFull(2, 1, 3) && shape.IsFull(0, 2, 1) && shape.IsFull(3, 0, 4),
            "Filled cells must be occupied.");
        Require(!shape.IsFull(1, 1, 1), "Unfilled cells must remain empty.");
        Require(shape.FirstFull(Direction.Axis.X) == 0 && shape.LastFull(Direction.Axis.X) == 4,
            "X cached bounds must span occupied cells.");
        Require(shape.FirstFull(Direction.Axis.Y) == 0 && shape.LastFull(Direction.Axis.Y) == 3,
            "Y cached bounds must span occupied cells.");
        Require(shape.FirstFull(Direction.Axis.Z) == 1 && shape.LastFull(Direction.Axis.Z) == 5,
            "Z cached bounds must span occupied cells.");
        Require(shape.FirstFull(Direction.Axis.X, 1, 3) == 2,
            "Per-line firstFull must scan coordinates along the selected axis.");
        Require(shape.LastFull(Direction.Axis.X, 1, 3) == 3,
            "Per-line lastFull must return the exclusive upper bound.");

        BitSetDiscreteVoxelShape filled = BitSetDiscreteVoxelShape.WithFilledBounds(
            4, 3, 2, 1, 1, 0, 4, 3, 2);
        Require(filled.FirstFull(Direction.Axis.X) == 1 && filled.LastFull(Direction.Axis.X) == 4,
            "WithFilledBounds must keep its provided X bounds.");
        Require(filled.FirstFull(Direction.Axis.Y) == 1 && filled.LastFull(Direction.Axis.Y) == 3,
            "WithFilledBounds must keep its provided Y bounds.");
        Require(filled.FirstFull(Direction.Axis.Z) == 0 && filled.LastFull(Direction.Axis.Z) == 2,
            "WithFilledBounds must keep its provided Z bounds.");
        Require(filled.IsFull(1, 1, 0) && filled.IsFull(3, 2, 1),
            "WithFilledBounds must fill the complete half-open range.");
        Require(!filled.IsFull(0, 1, 0) && !filled.IsFull(1, 0, 0) && !filled.IsFull(1, 1, 2),
            "Cells outside the half-open range must remain empty.");

        var copy = new BitSetDiscreteVoxelShape(filled);
        copy.Fill(0, 0, 0);
        Require(copy.IsFull(0, 0, 0) && !filled.IsFull(0, 0, 0),
            "Copy construction must clone occupancy storage.");
        Require(copy.FirstFull(Direction.Axis.X) == 0 && filled.FirstFull(Direction.Axis.X) == 1,
            "Copy bounds must be independently updated.");

        BitSetDiscreteVoxelShape solid = BitSetDiscreteVoxelShape.WithFilledBounds(3, 3, 3, 0, 0, 0, 3, 3, 3);
        Require(solid.IsInterior(1, 1, 1), "A cell and all six face-neighbors filled must be interior.");
        Require(!solid.IsInterior(0, 1, 1), "Boundary cells cannot be interior.");

        var shell = new BitSetDiscreteVoxelShape(3, 3, 3);
        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
        for (int z = 0; z < 3; z++)
        {
            if (x != 1 || y != 1 || z != 1)
                shell.Fill(x, y, z);
        }
        Require(!shell.IsInterior(1, 1, 1), "A missing face-neighbor must prevent interior classification.");

        var adjacent = new BitSetDiscreteVoxelShape(2, 1, 1);
        adjacent.Fill(0, 0, 0);
        adjacent.Fill(1, 0, 0);
        var cells = new List<(int, int, int, int, int, int)>();
        BitSetDiscreteVoxelShape.ForAllBoxes(adjacent,
            (x1, y1, z1, x2, y2, z2) => cells.Add((x1, y1, z1, x2, y2, z2)),
            mergeNeighbors: false);
        Require(cells.Count == 2 && cells[0] == (0, 0, 0, 1, 1, 1) && cells[1] == (1, 0, 0, 2, 1, 1),
            "Unmerged box traversal must emit cells in Y/X/Z order.");

        var merged = new List<(int, int, int, int, int, int)>();
        BitSetDiscreteVoxelShape.ForAllBoxes(adjacent,
            (x1, y1, z1, x2, y2, z2) => merged.Add((x1, y1, z1, x2, y2, z2)),
            mergeNeighbors: true);
        Require(merged.Count == 1 && merged[0] == (0, 0, 0, 2, 1, 1),
            "Greedy box merging must combine contiguous cells along X.");

        var separated = new BitSetDiscreteVoxelShape(3, 1, 1);
        separated.Fill(0, 0, 0);
        separated.Fill(2, 0, 0);
        var separateBoxes = new List<(int, int, int, int, int, int)>();
        BitSetDiscreteVoxelShape.ForAllBoxes(separated,
            (x1, y1, z1, x2, y2, z2) => separateBoxes.Add((x1, y1, z1, x2, y2, z2)),
            mergeNeighbors: true);
        Require(separateBoxes.Count == 2, "Disconnected occupied cells must remain separate boxes.");
        Require(adjacent.IsFull(0, 0, 0) && adjacent.IsFull(1, 0, 0),
            "Box enumeration must not mutate the input shape.");

        Require(!BooleanOp.FALSE.Apply(false, false) && BooleanOp.TRUE.Apply(true, false),
            "BooleanOp FALSE and TRUE must be constant functions.");
        Require(BooleanOp.AND.Apply(true, true) && !BooleanOp.AND.Apply(true, false),
            "BooleanOp AND must require both operands.");
        Require(BooleanOp.OR.Apply(true, false) && !BooleanOp.OR.Apply(false, false),
            "BooleanOp OR must accept either operand.");
        Require(BooleanOp.ONLY_FIRST.Apply(true, false) && !BooleanOp.ONLY_FIRST.Apply(true, true),
            "BooleanOp ONLY_FIRST must exclude the second operand.");
        Require(BooleanOp.ONLY_SECOND.Apply(false, true) && !BooleanOp.ONLY_SECOND.Apply(true, true),
            "BooleanOp ONLY_SECOND must exclude the first operand.");
        Require(BooleanOp.SAME.Apply(false, false) && !BooleanOp.SAME.Apply(true, false),
            "BooleanOp SAME must represent equivalence.");
        Require(BooleanOp.NOT_SAME.Apply(false, true) && !BooleanOp.NOT_SAME.Apply(true, true),
            "BooleanOp NOT_SAME must represent exclusive OR.");
        Require(BooleanOp.CAUSES.Apply(false, false) && !BooleanOp.CAUSES.Apply(true, false),
            "BooleanOp CAUSES must represent material implication.");
        Require(BooleanOp.CAUSED_BY.Apply(false, false) && !BooleanOp.CAUSED_BY.Apply(false, true),
            "BooleanOp CAUSED_BY must represent the converse implication.");
        Require(BooleanOp.NOT_OR.Apply(false, false) && !BooleanOp.NOT_OR.Apply(true, false),
            "BooleanOp NOT_OR must be the negated disjunction.");
        Require(BooleanOp.NOT_AND.Apply(false, true) && !BooleanOp.NOT_AND.Apply(true, true),
            "BooleanOp NOT_AND must be the negated conjunction.");
        Require(BooleanOp.NOT_FIRST.Apply(false, true) && !BooleanOp.NOT_FIRST.Apply(true, true),
            "BooleanOp NOT_FIRST must negate its first operand.");
        Require(BooleanOp.NOT_SECOND.Apply(true, false) && !BooleanOp.NOT_SECOND.Apply(true, true),
            "BooleanOp NOT_SECOND must negate its second operand.");
        Require(BooleanOp.FIRST.Apply(true, false) && !BooleanOp.FIRST.Apply(false, true),
            "BooleanOp FIRST must return its first operand.");
        Require(BooleanOp.SECOND.Apply(false, true) && !BooleanOp.SECOND.Apply(true, false),
            "BooleanOp SECOND must return its second operand.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
