using Asteria.Content.Chunks.Status;

internal static class ChunkDependenciesChecks
{
    public static void Run()
    {
        var dependencies = new ChunkDependencies(new[]
        {
            ChunkStatus.LIGHT,
            ChunkStatus.INITIALIZE_LIGHT,
            ChunkStatus.TERRAIN,
            ChunkStatus.BIOMES,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS,
            ChunkStatus.STRUCTURE_STARTS
        });

        Require(dependencies.Size() == 12 && dependencies.GetRadius() == 11,
            "The full generation dependency window must contain twelve rings, from distance zero through eleven.");
        Require(ReferenceEquals(dependencies.Get(0), ChunkStatus.LIGHT) &&
            ReferenceEquals(dependencies.Get(1), ChunkStatus.INITIALIZE_LIGHT) &&
            ReferenceEquals(dependencies.Get(2), ChunkStatus.TERRAIN) &&
            ReferenceEquals(dependencies.Get(3), ChunkStatus.BIOMES) &&
            ReferenceEquals(dependencies.Get(11), ChunkStatus.STRUCTURE_STARTS),
            "ChunkDependencies.get must resolve each distance to the exact status instance stored at that ring.");

        Require(dependencies.GetRadiusOf(ChunkStatus.EMPTY) == 11 &&
            dependencies.GetRadiusOf(ChunkStatus.STRUCTURE_STARTS) == 11 &&
            dependencies.GetRadiusOf(ChunkStatus.STRUCTURE_REFERENCES) == 3 &&
            dependencies.GetRadiusOf(ChunkStatus.BIOMES) == 3 &&
            dependencies.GetRadiusOf(ChunkStatus.TERRAIN) == 2 &&
            dependencies.GetRadiusOf(ChunkStatus.FEATURES) == 1 &&
            dependencies.GetRadiusOf(ChunkStatus.INITIALIZE_LIGHT) == 1 &&
            dependencies.GetRadiusOf(ChunkStatus.LIGHT) == 0 &&
            dependencies.GetRadiusOf(ChunkStatus.FULL) == -1,
            "getRadiusOf must return the farthest ring guaranteed to have reached a status, matching the vanilla FULL pyramid.");

        IReadOnlyList<ChunkStatus> list = dependencies.AsList();
        Require(ReferenceEquals(list, dependencies.AsList()) && list.Count == dependencies.Size(),
            "asList must expose the same immutable dependency list instance.");
        Require(dependencies.ToString() == "[asteria:light, asteria:initialize_light, asteria:terrain, asteria:biomes, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts, asteria:structure_starts]",
            "ChunkDependencies.toString must display its status sequence in distance order.");

        var empty = new ChunkDependencies(Array.Empty<ChunkStatus>());
        Require(empty.Size() == 0 && empty.GetRadius() == -1 &&
            empty.GetRadiusOf(ChunkStatus.EMPTY) == -1 && empty.GetRadiusOf(ChunkStatus.FULL) == -1,
            "An empty dependency window must have radius -1 and guarantee no status.");

        RequireThrows<ArgumentOutOfRangeException>(() => dependencies.Get(-1),
            "A negative distance must retain the immutable-list index failure.");
        RequireThrows<ArgumentOutOfRangeException>(() => dependencies.Get(dependencies.Size()),
            "A distance outside the dependency list must retain the immutable-list index failure.");
        RequireThrows<ArgumentNullException>(() => dependencies.GetRadiusOf(null!),
            "A null status cannot be used to query a dependency radius.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
}
