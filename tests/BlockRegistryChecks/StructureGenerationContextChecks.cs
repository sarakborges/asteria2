using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;

internal static class StructureGenerationContextChecks
{
    public static void Run()
    {
        CheckSeededStructureRandom();
        CheckStubBiomeValidationAtQuartCoordinates();
        CheckColumnQuartRangeIsInclusiveAndShortCircuits();
        CheckTerrainColumnUsesBuildHeightBounds();
        CheckConstructorRejectsMissingDependencies();
    }

    private static void CheckSeededStructureRandom()
    {
        const long seed = 0x535452554354524EL;
        ChunkPos chunkPosition = new(-13, 27);
        OverworldWorld world = CreateWorld(seed);
        var resolver = new RecordingBiomeResolver((_, _, _) =>
            Biomes.PLAINS.BuiltInRegistryHolder);

        var context = new Structure.GenerationContext(
            world.Generator,
            resolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            LevelHeightAccessor.Create(-16, 64),
            static _ => true);

        var expectedRandom = new WorldgenRandom(new LegacyRandomSource(0L));
        expectedRandom.SetLargeFeatureSeed(seed, chunkPosition.X, chunkPosition.Z);

        int expectedFirst = expectedRandom.NextInt();
        int expectedSecond = expectedRandom.NextInt();

        Require(
            context.Seed == seed &&
            context.ChunkPos == chunkPosition &&
            context.Random.NextInt() == expectedFirst &&
            context.Random.NextInt() == expectedSecond,
            "GenerationContext must seed its WorldgenRandom with the level seed and exact chunk coordinates.");
        Require(
            ReferenceEquals(context.Generator, world.Generator) &&
            ReferenceEquals(context.RandomState, world.Generator.RandomState) &&
            ReferenceEquals(context.BiomeResolver, resolver),
            "GenerationContext must retain the supplied generator, random state and biome resolver instances.");
    }

    private static void CheckStubBiomeValidationAtQuartCoordinates()
    {
        OverworldWorld world = CreateWorld(0x56414C494442494FL);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var resolver = new RecordingBiomeResolver((quartX, quartY, quartZ) =>
            quartX == -3 && quartY == -2 && quartZ == 4 ? desert : plains);

        var context = new Structure.GenerationContext(
            world.Generator,
            resolver,
            world.Generator.RandomState,
            world.Seed,
            new ChunkPos(-1, 2),
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        var acceptedStub = new Structure.GenerationStub(
            new BlockPos(-12, -8, 16),
            new StructurePiecesBuilder());
        var rejectedStub = new Structure.GenerationStub(
            new BlockPos(-11, -8, 16),
            new StructurePiecesBuilder());

        Require(
            context.IsValidBiome(acceptedStub),
            "IsValidBiome must resolve the stub block position at quart coordinates before applying the biome predicate.");
        Require(
            !context.IsValidBiome(rejectedStub),
            "IsValidBiome must reject a stub when the resolved biome does not satisfy the valid-biome predicate.");
        Require(
            resolver.Queries.SequenceEqual(new[] { (-3, -2, 4), (-3, -2, 4) }),
            "Negative block coordinates must be converted to quart coordinates with arithmetic shift semantics.");
    }

    private static void CheckColumnQuartRangeIsInclusiveAndShortCircuits()
    {
        OverworldWorld world = CreateWorld(0x434F4C554D4E4249L);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;

        var matchingLastQuart = new RecordingBiomeResolver((_, quartY, _) =>
            quartY == 2 ? desert : plains);
        var context = new Structure.GenerationContext(
            world.Generator,
            matchingLastQuart,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        Require(
            context.CouldStructureExistInColumn(
                blockX: -1,
                blockZ: 8,
                minBlockY: -5,
                maxBlockY: 8),
            "Column biome validation must include the upper quart derived from maxBlockY.");
        Require(
            matchingLastQuart.Queries.SequenceEqual(new[]
            {
                (-1, -2, 2),
                (-1, -1, 2),
                (-1, 0, 2),
                (-1, 1, 2),
                (-1, 2, 2)
            }),
            "Column validation must sample quart-Y in ascending order across the inclusive converted block interval.");

        var matchingFirstQuart = new RecordingBiomeResolver((_, quartY, _) =>
            quartY == -2 ? desert : plains);
        var shortCircuitContext = new Structure.GenerationContext(
            world.Generator,
            matchingFirstQuart,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        Require(
            shortCircuitContext.CouldStructureExistInColumn(-1, 8, -5, 8) &&
            matchingFirstQuart.Queries.Count == 1 &&
            matchingFirstQuart.Queries[0] == (-1, -2, 2),
            "Column validation must stop at the first matching valid biome.");
        
        var noMatchResolver = new RecordingBiomeResolver((_, _, _) => plains);
        var noMatchContext = new Structure.GenerationContext(
            world.Generator,
            noMatchResolver,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        Require(
            !noMatchContext.CouldStructureExistInColumn(-1, 8, -5, 8) &&
            noMatchResolver.Queries.Count == 5,
            "Column validation must return false only after every quart-Y in the inclusive interval has been checked.");
    }

    private static void CheckTerrainColumnUsesBuildHeightBounds()
    {
        OverworldWorld world = CreateWorld(0x5445525241494E43L);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var resolver = new RecordingBiomeResolver((_, quartY, _) =>
            quartY == 11 ? desert : plains);

        var context = new Structure.GenerationContext(
            world.Generator,
            resolver,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        Require(
            context.CouldValidBiomeExistInTerrainColumn(3, -5),
            "Terrain-column validation must include the quart containing max build height.");

        Require(
            resolver.Queries.SequenceEqual(
                Enumerable.Range(-5, 17).Select(quartY => (0, quartY, -2))),
            "Terrain-column validation must start at minY - 1 and continue through maxY, both converted to quart coordinates.");
    }

    private static void CheckConstructorRejectsMissingDependencies()
    {
        OverworldWorld world = CreateWorld(0x4E554C4C434F4E54L);
        ChunkPos position = ChunkPos.Zero;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        Predicate<Holder<Biome>> predicate = static _ => true;
        BiomeResolver resolver = new RecordingBiomeResolver((_, _, _) =>
            Biomes.PLAINS.BuiltInRegistryHolder);

        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                null!,
                resolver,
                world.Generator.RandomState,
                world.Seed,
                position,
                heightAccessor,
                predicate),
            "GenerationContext must reject a null chunk generator.");
        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                world.Generator,
                null!,
                world.Generator.RandomState,
                world.Seed,
                position,
                heightAccessor,
                predicate),
            "GenerationContext must reject a null biome resolver.");
        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                world.Generator,
                resolver,
                null!,
                world.Seed,
                position,
                heightAccessor,
                predicate),
            "GenerationContext must reject a null random state.");
        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                world.Generator,
                resolver,
                world.Generator.RandomState,
                world.Seed,
                null!,
                heightAccessor,
                predicate),
            "GenerationContext must reject a null chunk position.");
        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                world.Generator,
                resolver,
                world.Generator.RandomState,
                world.Seed,
                position,
                null!,
                predicate),
            "GenerationContext must reject a null height accessor.");
        RequireThrows<ArgumentNullException>(
            () => new Structure.GenerationContext(
                world.Generator,
                resolver,
                world.Generator.RandomState,
                world.Seed,
                position,
                heightAccessor,
                null!),
            "GenerationContext must reject a null valid-biome predicate.");
    }

    private static OverworldWorld CreateWorld(long seed) =>
        new(seed);

    private sealed class RecordingBiomeResolver(
        Func<int, int, int, Holder<Biome>> resolve) : BiomeResolver
    {
        public List<(int QuartX, int QuartY, int QuartZ)> Queries { get; } = new();

        public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
        {
            Queries.Add((quartX, quartY, quartZ));
            return resolve(quartX, quartY, quartZ);
        }
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
