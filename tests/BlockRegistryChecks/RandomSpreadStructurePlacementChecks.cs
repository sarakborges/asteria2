using Asteria.Core;
using Asteria.World;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure.Placement;

internal static class RandomSpreadStructurePlacementChecks
{
    public static void Run()
    {
        CheckPotentialChunkMatchesTheReferenceFormula();
        CheckNegativeCoordinatesUseFloorDivision();
        CheckCandidateIsStableForSeedAndCell();
        CheckInvalidParametersFailAtUseTime();
    }

    private static void CheckPotentialChunkMatchesTheReferenceFormula()
    {
        const long seed = 0x4D43535452554354L;
        const int spacing = 37;
        const int separation = 9;
        const int salt = 10387320;
        const int sourceX = 147;
        const int sourceZ = -206;

        var placement = new RandomSpreadStructurePlacement(
            spacing,
            separation,
            RandomSpreadType.TRIANGULAR,
            salt);

        ChunkPos expected = CalculateReference(
            seed,
            sourceX,
            sourceZ,
            spacing,
            separation,
            RandomSpreadType.TRIANGULAR,
            salt);
        ChunkPos actual = placement.GetPotentialStructureChunk(
            seed,
            sourceX,
            sourceZ);

        Require(
            actual == expected,
            "The candidate chunk must match the vanilla floor-grid, large-feature seed and spread evaluation formula.");
    }

    private static void CheckNegativeCoordinatesUseFloorDivision()
    {
        const int spacing = 32;
        const int separation = 8;
        const long seed = 918273645L;

        var placement = new RandomSpreadStructurePlacement(
            spacing,
            separation,
            RandomSpreadType.LINEAR,
            17);

        ChunkPos actual = placement.GetPotentialStructureChunk(seed, -1, -33);
        ChunkPos expected = CalculateReference(
            seed,
            -1,
            -33,
            spacing,
            separation,
            RandomSpreadType.LINEAR,
            17);

        Require(
            actual == expected &&
            actual.X >= -spacing &&
            actual.X < 0 &&
            actual.Z >= -2 * spacing &&
            actual.Z < -spacing,
            "Negative source coordinates must use floor division and remain in their containing spacing cells.");
    }

    private static void CheckCandidateIsStableForSeedAndCell()
    {
        var placement = new RandomSpreadStructurePlacement(
            28,
            5,
            RandomSpreadType.TRIANGULAR,
            10387320);

        ChunkPos first = placement.GetPotentialStructureChunk(123456789L, 65, 97);
        ChunkPos repeated = placement.GetPotentialStructureChunk(123456789L, 65, 97);
        ChunkPos sameCell = placement.GetPotentialStructureChunk(123456789L, 79, 110);
        ChunkPos otherSeed = placement.GetPotentialStructureChunk(123456790L, 65, 97);

        Require(
            first == repeated,
            "A fixed seed and spacing-grid cell must always produce the same candidate.");
        Require(
            first == sameCell,
            "Every source coordinate inside one spacing-grid cell must resolve to that cell's same candidate.");
        Require(
            first != otherSeed,
            "Changing the level seed must change the deterministic candidate for this regression fixture.");
    }

    private static void CheckInvalidParametersFailAtUseTime()
    {
        var zeroSpacing = new RandomSpreadStructurePlacement(
            0,
            0,
            RandomSpreadType.LINEAR,
            0);

        RequireThrows<DivideByZeroException>(
            () => zeroSpacing.GetPotentialStructureChunk(0L, 1, 1),
            "The direct constructor must not silently normalize zero spacing before floor division.");

        var zeroSpreadRange = new RandomSpreadStructurePlacement(
            8,
            8,
            RandomSpreadType.LINEAR,
            0);

        RequireThrows<ArgumentException>(
            () => zeroSpreadRange.GetPotentialStructureChunk(0L, 1, 1),
            "The direct constructor must preserve the random source's failure when spacing minus separation is zero.");
    }

    private static ChunkPos CalculateReference(
        long seed,
        int sourceX,
        int sourceZ,
        int spacing,
        int separation,
        RandomSpreadType spreadType,
        int salt)
    {
        int gridX = Mth.FloorDiv(sourceX, spacing);
        int gridZ = Mth.FloorDiv(sourceZ, spacing);
        var random = new WorldgenRandom(new LegacyRandomSource(0L));
        random.SetLargeFeatureWithSalt(seed, gridX, gridZ, salt);

        int limit = spacing - separation;
        int spreadX = spreadType.Evaluate(random, limit);
        int spreadZ = spreadType.Evaluate(random, limit);

        return new ChunkPos(
            unchecked(gridX * spacing + spreadX),
            unchecked(gridZ * spacing + spreadZ));
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
