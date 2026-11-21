using Asteria.Core.Shapes;

internal static class IndexMergerChecks
{
    public static void Run()
    {
        CheckCubePointRanges();
        CheckIdenticalMerger();
        CheckNonOverlappingMerger();
        CheckIndirectMerger();
        CheckDiscreteCubeMerger();
        CheckMergerSelection();
    }

    private static void CheckCubePointRanges()
    {
        var thirds = new CubePointRange(3);
        Require(thirds.Count == 4, "A cube point range must contain parts plus one endpoint.");
        Require(thirds.GetDouble(0) == 0.0, "Cube point ranges must start at zero.");
        Require(thirds.GetDouble(1) == 1.0 / 3.0, "Cube point ranges must divide coordinates evenly.");
        Require(thirds.GetDouble(3) == 1.0, "Cube point ranges must end at one.");
        RequireThrows<ArgumentException>(() => _ = new CubePointRange(0),
            "Cube point ranges must reject zero parts.");
        RequireThrows<ArgumentOutOfRangeException>(() => thirds.GetDouble(4),
            "Cube point range lookup must validate the coordinate index.");
    }

    private static void CheckIdenticalMerger()
    {
        var coords = new ArrayDoubleList(0.0, 0.25, 0.75, 1.0);
        var merger = new IdenticalMerger(coords);
        var mappings = new List<(int First, int Second, int Result)>();
        Require(merger.ForMergedIndexes((first, second, result) =>
        {
            mappings.Add((first, second, result));
            return true;
        }), "Identical merger must complete when every consumer call continues.");

        Require(merger.Size() == 4 && ReferenceEquals(merger.GetList(), coords),
            "Identical merger must preserve the coordinate list.");
        Require(
            mappings.SequenceEqual(new[] { (0, 0, 0), (1, 1, 1), (2, 2, 2) }),
            "Identical merger must map matching intervals to identical source indexes.");

        bool stopped = merger.ForMergedIndexes((_, _, result) => result < 1);
        Require(!stopped, "Index merger must propagate an early consumer stop.");
    }

    private static void CheckNonOverlappingMerger()
    {
        var lower = new ArrayDoubleList(0.0, 0.25);
        var upper = new ArrayDoubleList(0.75, 1.0);
        var merger = new NonOverlappingMerger(lower, upper, swap: false);
        var mappings = new List<(int First, int Second, int Result)>();
        merger.ForMergedIndexes((first, second, result) =>
        {
            mappings.Add((first, second, result));
            return true;
        });

        Require(merger.Size() == 4, "Non-overlapping merger must concatenate both coordinate ranges.");
        Require(
            Enumerable.Range(0, merger.Size()).Select(merger.GetList().GetDouble)
                .SequenceEqual(new[] { 0.0, 0.25, 0.75, 1.0 }),
            "Non-overlapping merger coordinates must retain their ordered lower/upper values.");
        Require(
            mappings.SequenceEqual(new[] { (0, -1, 0), (1, -1, 1), (1, 0, 2) }),
            "Non-overlapping merger must map lower intervals before upper intervals.");

        var swapped = new NonOverlappingMerger(
            new ArrayDoubleList(0.0, 0.25),
            new ArrayDoubleList(0.75, 1.0),
            swap: true);
        var swappedMappings = new List<(int First, int Second, int Result)>();
        swapped.ForMergedIndexes((first, second, result) =>
        {
            swappedMappings.Add((first, second, result));
            return true;
        });
        Require(
            swappedMappings.SequenceEqual(new[] { (-1, 0, 0), (-1, 1, 1), (0, 1, 2) }),
            "Swapped non-overlapping merger must transpose the source indexes.");
    }

    private static void CheckIndirectMerger()
    {
        var first = new ArrayDoubleList(0.0, 0.5, 1.0);
        var second = new ArrayDoubleList(0.0, 0.25, 0.75, 1.0);
        var merger = new IndirectMerger(first, second, firstOnlyMatters: true, secondOnlyMatters: true);
        var mappings = new List<(int First, int Second, int Result)>();
        merger.ForMergedIndexes((firstIndex, secondIndex, resultIndex) =>
        {
            mappings.Add((firstIndex, secondIndex, resultIndex));
            return true;
        });

        Require(merger.Size() == 5, "Indirect merger must deduplicate the two sorted coordinate sequences.");
        double[] expected = { 0.0, 0.25, 0.5, 0.75, 1.0 };
        Require(
            Enumerable.Range(0, merger.Size()).Select(merger.GetList().GetDouble).SequenceEqual(expected),
            "Indirect merger must return the ordered union of coordinate values.");
        Require(
            mappings.SequenceEqual(new[] { (0, 0, 0), (0, 1, 1), (1, 1, 2), (1, 2, 3) }),
            "Indirect merger must preserve the source interval indexes for each merged interval.");

        var closeFirst = new ArrayDoubleList(0.0, 0.5, 1.0);
        var closeSecond = new ArrayDoubleList(0.0, 0.50000005, 1.0);
        var close = new IndirectMerger(closeFirst, closeSecond, firstOnlyMatters: true, secondOnlyMatters: true);
        Require(close.Size() == 3, "Coordinates within the merge epsilon must collapse to one grid boundary.");
        Require(close.GetList().GetDouble(1) == 0.5,
            "The selected representative at a deduplicated boundary must follow the merger's source ordering.");
    }

    private static void CheckDiscreteCubeMerger()
    {
        var merger = new DiscreteCubeMerger(2, 3);
        Require(merger.Size() == 7, "Discrete cube merger must use the least common multiple of the subdivisions.");
        double[] coords = Enumerable.Range(0, 7).Select(merger.GetList().GetDouble).ToArray();
        Require(coords.SequenceEqual(new[] { 0.0, 1.0 / 6.0, 2.0 / 6.0, 3.0 / 6.0, 4.0 / 6.0, 5.0 / 6.0, 1.0 }),
            "Discrete cube merger must produce the uniform common coordinate grid.");

        var mappings = new List<(int First, int Second, int Result)>();
        merger.ForMergedIndexes((first, second, result) =>
        {
            mappings.Add((first, second, result));
            return true;
        });
        Require(
            mappings.SequenceEqual(new[] { (0, 0, 0), (0, 0, 1), (0, 1, 2), (1, 1, 3), (1, 2, 4), (1, 2, 5) }),
            "Discrete cube merger must map each common interval to its source cell indexes.");
    }

    private static void CheckMergerSelection()
    {
        IndexMerger uniform = IndexMergers.Create(
            1, new CubePointRange(2), new CubePointRange(3),
            firstOnlyMatters: true, secondOnlyMatters: true);
        Require(uniform is DiscreteCubeMerger,
            "Small evenly divided coordinate grids must choose the discrete cube merger.");

        IndexMerger large = IndexMergers.Create(
            100, new CubePointRange(2), new CubePointRange(3),
            firstOnlyMatters: true, secondOnlyMatters: true);
        Require(large is IndirectMerger,
            "A subdivision grid whose cost exceeds 256 must choose the indirect merger.");

        IndexMerger separated = IndexMergers.Create(
            1, new ArrayDoubleList(0.0, 0.25), new ArrayDoubleList(0.75, 1.0),
            firstOnlyMatters: true, secondOnlyMatters: true);
        Require(separated is NonOverlappingMerger,
            "Strictly separated coordinate ranges must choose the non-overlapping merger.");

        IndexMerger identical = IndexMergers.Create(
            1, new ArrayDoubleList(0.0, 0.5, 1.0), new ArrayDoubleList(0.0, 0.5, 1.0),
            firstOnlyMatters: true, secondOnlyMatters: true);
        Require(identical is IdenticalMerger,
            "Equal coordinate sequences with equal interval counts must choose the identical merger.");

        IndexMerger overlapping = IndexMergers.Create(
            1, new ArrayDoubleList(0.0, 0.5, 1.0), new ArrayDoubleList(0.0, 0.25, 0.75, 1.0),
            firstOnlyMatters: true, secondOnlyMatters: true);
        Require(overlapping is IndirectMerger,
            "Overlapping non-identical coordinate grids must choose the indirect merger.");
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
