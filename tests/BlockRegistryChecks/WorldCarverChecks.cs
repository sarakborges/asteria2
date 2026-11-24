using Asteria.Content.Chunks;
using Asteria.World;
using Asteria.World.LevelGen;

internal static class WorldCarverChecks
{
    private sealed class RecordingCarverOutput : CarverOutput
    {
        public int MinimumY { get; }
        public int MaximumY { get; }
        public HashSet<(int X, int Y, int Z)> Positions { get; } = new();

        public RecordingCarverOutput(int minimumY, int maximumY)
        {
            MinimumY = minimumY;
            MaximumY = maximumY;
        }

        public void Carve(int x, int y, int z) =>
            Positions.Add((x, y, z));

        public int MinY() => MinimumY;

        public int MaxY() => MaximumY;
    }

    public static void Run()
    {
        CheckEllipsoidInteriorAndVerticalClipping();
        CheckTargetChunkHorizontalClipping();
        CheckSkipPredicate();
        CheckCanReach();
    }

    private static void CheckEllipsoidInteriorAndVerticalClipping()
    {
        var output = new RecordingCarverOutput(0, 15);

        WorldCarver.CarveEllipsoid(
            ChunkPos.Zero,
            x: 8.0,
            y: 8.0,
            z: 8.0,
            horizontalRadius: 3.0,
            verticalRadius: 3.0,
            output,
            static (_, _, _, _) => false);

        Require(output.Positions.Count > 0,
            "An ellipsoid centered inside the target chunk must mark carved positions.");
        Require(output.Positions.Contains((8, 8, 8)),
            "The ellipsoid must include its center block.");
        Require(output.Positions.All(position =>
                position.X is >= 0 and <= 15 &&
                position.Z is >= 0 and <= 15 &&
                position.Y is > 4 and <= 12),
            "The ellipsoid must stay inside its horizontal chunk bounds and preserve the exclusive minY loop boundary.");
    }

    private static void CheckTargetChunkHorizontalClipping()
    {
        var output = new RecordingCarverOutput(2, 7);

        WorldCarver.CarveEllipsoid(
            ChunkPos.Zero,
            x: 0.5,
            y: 5.0,
            z: 15.5,
            horizontalRadius: 4.0,
            verticalRadius: 5.0,
            output,
            static (_, _, _, _) => false);

        Require(output.Positions.Contains((0, 7, 15)),
            "A clipped ellipsoid must preserve a carved point on the target chunk's horizontal edge and output maximum Y.");
        Require(output.Positions.All(position =>
                position.X is >= 0 and <= 15 &&
                position.Z is >= 0 and <= 15 &&
                position.Y is > 2 and <= 7),
            "CarveEllipsoid must clamp X/Z to the target chunk and Y to the CarverOutput bounds.");
    }

    private static void CheckSkipPredicate()
    {
        var output = new RecordingCarverOutput(0, 15);

        WorldCarver.CarveEllipsoid(
            ChunkPos.Zero,
            x: 8.0,
            y: 8.0,
            z: 8.0,
            horizontalRadius: 3.0,
            verticalRadius: 3.0,
            output,
            static (_, _, _, worldY) => worldY == 8);

        Require(output.Positions.Count > 0,
            "A skip predicate must not suppress unrelated positions in the ellipsoid.");
        Require(output.Positions.All(position => position.Y != 8),
            "CarveEllipsoid must delegate every candidate block to the skip predicate before marking it.");
    }

    private static void CheckCanReach()
    {
        Require(
            WorldCarver.CanReach(ChunkPos.Zero, 8.0, 8.0, currentStep: 0, totalSteps: 10, thickness: 1.0f),
            "A carver at the target chunk center must be able to reach it.");
        Require(
            !WorldCarver.CanReach(ChunkPos.Zero, 100.0, 100.0, currentStep: 0, totalSteps: 10, thickness: 1.0f),
            "A distant carver position outside its remaining reach must be rejected.");
        Require(
            WorldCarver.CanReach(
                ChunkPos.Zero,
                x: 65_544.0,
                z: 8.0,
                currentStep: 0,
                totalSteps: 65_536,
                thickness: 1.0f),
            "CanReach must square remaining steps as double and avoid integer overflow on long paths.");


        var output = new RecordingCarverOutput(0, 15);
        WorldCarver.CarveEllipsoid(
            ChunkPos.Zero,
            x: 200.0,
            y: 8.0,
            z: 8.0,
            horizontalRadius: 3.0,
            verticalRadius: 3.0,
            output,
            static (_, _, _, _) => false);
        Require(output.Positions.Count == 0,
            "An ellipsoid fully outside the target chunk's maximum horizontal delta must do no work.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
