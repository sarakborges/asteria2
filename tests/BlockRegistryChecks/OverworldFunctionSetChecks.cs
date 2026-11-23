using Asteria.World.LevelGen;

internal static class OverworldFunctionSetChecks
{
    public static void Run()
    {
        var source = new OverworldFunctionSet<int>(
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
        var mapped = source.Map(static value => $"density/{value}");

        Require(
            mapped == new OverworldFunctionSet<string>(
                "density/1",
                "density/2",
                "density/3",
                "density/4",
                "density/5",
                "density/6",
                "density/7",
                "density/8",
                "density/9",
                "density/10",
                "density/11",
                "density/12"),
            "OverworldFunctionSet.map must preserve all twelve vanilla slots and their declared order.");

        var identity = source.Map(static value => value);
        Require(
            identity == source,
            "Mapping an OverworldFunctionSet with the identity function must preserve its complete value.");

        try
        {
            source.Map<int>(null!);
        }
        catch (ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException("OverworldFunctionSet.map must reject a null mapper.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
