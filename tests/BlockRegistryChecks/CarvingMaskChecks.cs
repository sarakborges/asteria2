using Asteria.Content.Chunks;

internal static class CarvingMaskChecks
{
    private readonly record struct Segment(int X, int Z, int BottomY, int TopY);

    public static void Run()
    {
        CheckBoundsAndEmptyState();
        CheckContiguousVerticalSegments();
        CheckRunsAcrossColumnBoundaries();
        CheckPackedCoordinateIndexing();
        CheckNegativeBitIndexRejection();
    }

    private static void CheckBoundsAndEmptyState()
    {
        var mask = new CarvingMask(-64, 319);

        Require(mask.MinY() == -64 && mask.MaxY() == 319,
            "CarvingMask must expose its inclusive Y bounds.");
        Require(mask.IsEmpty(),
            "A new CarvingMask must contain no carved positions.");

        mask.Carve(3, -64, 9);
        Require(!mask.IsEmpty(),
            "Carving the lowest supported Y position must make the mask non-empty.");
    }

    private static void CheckContiguousVerticalSegments()
    {
        var mask = new CarvingMask(-8, 7);
        mask.Carve(2, -7, 4);
        mask.Carve(2, -6, 4);
        mask.Carve(2, -4, 4);
        mask.Carve(2, -3, 4);
        mask.Carve(2, -3, 4);

        var segments = new List<Segment>();
        mask.Visit((x, z, bottomY, topY) =>
            segments.Add(new Segment(x, z, bottomY, topY)));

        Require(
            segments.SequenceEqual(new[]
            {
                new Segment(2, 4, -7, -6),
                new Segment(2, 4, -4, -3)
            }),
            "Visit must group consecutive carved Y bits in one column and split at each uncarved bit.");
    }

    private static void CheckRunsAcrossColumnBoundaries()
    {
        var mask = new CarvingMask(-8, 7);

        // With a 16-block height, these occupy adjacent mask bits 15 and 16.
        // The first bit is the top of column (0, 0); the second is the bottom
        // of the next column (0, 1).
        mask.Carve(0, 7, 0);
        mask.Carve(0, -8, 1);

        var segments = new List<Segment>();
        mask.Visit((x, z, bottomY, topY) =>
            segments.Add(new Segment(x, z, bottomY, topY)));

        Require(
            segments.SequenceEqual(new[]
            {
                new Segment(0, 0, 7, 7),
                new Segment(0, 1, -8, -8)
            }),
            "A contiguous bit run spanning two columns must be split into the matching column Y intervals.");
    }

    private static void CheckPackedCoordinateIndexing()
    {
        var mask = new CarvingMask(-8, 7);

        // Carving does not validate the supplied Y against maxY. The reference
        // maps coordinates directly to the linear bit index, so Y=8 aliases
        // the first Y bit of the next Z column.
        mask.Carve(0, 8, 0);

        Segment? observed = null;
        mask.Visit((x, z, bottomY, topY) =>
            observed = new Segment(x, z, bottomY, topY));

        Require(
            observed == new Segment(0, 1, -8, -8),
            "CarvingMask must preserve the reference's unchecked packed-index mapping.");
    }

    private static void CheckNegativeBitIndexRejection()
    {
        var mask = new CarvingMask(-8, 7);
        RequireThrows<ArgumentOutOfRangeException>(
            () => mask.Carve(0, -9, 0),
            "A negative linear bit index must be rejected by the mask storage.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
