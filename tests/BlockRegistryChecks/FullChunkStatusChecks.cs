using Asteria.Server.Level;

internal static class FullChunkStatusChecks
{
    public static void Run()
    {
        FullChunkStatus[] statuses = Enum.GetValues<FullChunkStatus>();
        FullChunkStatus[] expected =
        [
            FullChunkStatus.INACCESSIBLE,
            FullChunkStatus.FULL,
            FullChunkStatus.BLOCK_TICKING,
            FullChunkStatus.ENTITY_TICKING
        ];

        Require(
            statuses.SequenceEqual(expected),
            "FullChunkStatus must preserve the four Minecraft 26.3 values and their declaration order.");

        for (int statusIndex = 0; statusIndex < expected.Length; statusIndex++)
        {
            for (int stepIndex = 0; stepIndex < expected.Length; stepIndex++)
            {
                FullChunkStatus status = expected[statusIndex];
                FullChunkStatus step = expected[stepIndex];
                Require(
                    status.IsOrAfter(step) == (statusIndex >= stepIndex),
                    $"FullChunkStatus.IsOrAfter must preserve ordinal comparison for {status} and {step}.");
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
