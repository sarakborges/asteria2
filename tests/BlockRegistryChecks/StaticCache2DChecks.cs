using Asteria.Util;

internal static class StaticCache2DChecks
{
    public static void Run()
    {
        var initializationOrder = new List<(int X, int Z)>();
        StaticCache2D<int> cache = StaticCache2D<int>.Create(10, 20, 1, (x, z) =>
        {
            initializationOrder.Add((x, z));
            return unchecked(x * 100 + z);
        });

        var expectedOrder = new List<(int X, int Z)>();
        for (int x = 9; x <= 11; x++)
        {
            for (int z = 19; z <= 21; z++)
                expectedOrder.Add((x, z));
        }

        Require(initializationOrder.SequenceEqual(expectedOrder),
            "StaticCache2D must initialize its rectangular cache in X-major, then Z order.");
        Require(cache.Contains(9, 19) && cache.Contains(11, 21) &&
            !cache.Contains(8, 19) && !cache.Contains(12, 20) && !cache.Contains(10, 22),
            "StaticCache2D.contains must include both lower bounds and exclude the upper bounds.");
        Require(cache.Get(9, 19) == 919 && cache.Get(10, 20) == 1020 && cache.Get(11, 21) == 1121,
            "StaticCache2D.get must use X-major, Z-minor indexing.");
        Require(cache.ToString() == "StaticCache2D[9, 19, 12, 22]",
            "StaticCache2D.toString must report min coordinates and exclusive upper bounds.");

        var foreachValues = new List<int>();
        cache.ForEach(foreachValues.Add);
        Require(foreachValues.SequenceEqual(expectedOrder.Select(position => position.X * 100 + position.Z)),
            "StaticCache2D.forEach must traverse its backing cache in storage order.");

        var mappingOrder = new List<(int X, int Z)>();
        StaticCache2D<string> mapped = cache.Map((value, x, z) =>
        {
            mappingOrder.Add((x, z));
            return $"{x}:{z}:{value}";
        });
        Require(mappingOrder.SequenceEqual(expectedOrder),
            "StaticCache2D.map must evaluate the mapping function in the same X-major, Z-minor order.");
        Require(mapped.Get(10, 20) == "10:20:1020" && mapped.Get(9, 21) == "9:21:921",
            "StaticCache2D.map must preserve bounds and pass each cached value with its coordinates.");

        ArgumentException outOfRange = Capture<ArgumentException>(
            () => cache.Get(8, 20),
            "StaticCache2D.get must reject coordinates outside the fixed cache.");
        Require(outOfRange.GetType() == typeof(ArgumentException) &&
            outOfRange.Message.StartsWith(
                "Requested out of range value (8,20) from StaticCache2D[9, 19, 12, 22]",
                StringComparison.Ordinal),
            "Out-of-range reads must preserve the vanilla exception type and diagnostic prefix.");

        StaticCache2D<int> single = StaticCache2D<int>.Create(-4, 7, 0, (x, z) => x + z);
        Require(single.Contains(-4, 7) && !single.Contains(-3, 7) && single.Get(-4, 7) == 3,
            "A zero-radius cache must contain exactly its center coordinate.");
    }

    private static TException Capture<TException>(Action action, string message)
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
