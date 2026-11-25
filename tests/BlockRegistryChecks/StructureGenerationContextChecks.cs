using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;

internal static class StructureGenerationContextChecks
{
    public static void Run()
    {
        CheckSeededStructureRandom();
        CheckStubBiomeValidationAtQuartCoordinates();
        CheckColumnQuartRangeIsInclusiveAndShortCircuits();
        CheckTerrainColumnUsesBuildHeightBounds();
        CheckChunkCenterBiomeValidationUsesMiddleColumn();
        CheckFindValidGenerationPointFiltersByBiome();
        CheckOnTopOfChunkCenterHelpers();
        CheckStructureStartGenerationFromStub();
        CheckStructureHeightSamplingHelpers();
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

    private static void CheckChunkCenterBiomeValidationUsesMiddleColumn()
    {
        OverworldWorld world = CreateWorld(0x43454E5445524249L);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var resolver = new RecordingBiomeResolver((quartX, quartY, quartZ) =>
            quartX == -2 && quartY == 11 && quartZ == 10 ? desert : plains);

        var context = new Structure.GenerationContext(
            world.Generator,
            resolver,
            world.Generator.RandomState,
            world.Seed,
            new ChunkPos(-1, 2),
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));

        Require(
            context.CouldValidBiomeExistOnTopOfChunkCenter(),
            "Chunk-center validation must inspect the full terrain column at the chunk's middle block coordinates.");

        Require(
            resolver.Queries.SequenceEqual(
                Enumerable.Range(-5, 17).Select(quartY => (-2, quartY, 10))),
            "Chunk-center validation must use the middle column, convert negative block coordinates to quart coordinates, and include the full build-height interval.");
    }

    private static void CheckFindValidGenerationPointFiltersByBiome()
    {
        OverworldWorld world = CreateWorld(0x5354554256414C49L);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var start = new BlockPos(-12, -8, 16);
        var stub = new Structure.GenerationStub(start, new StructurePiecesBuilder());

        var validResolver = new RecordingBiomeResolver((quartX, quartY, quartZ) =>
            quartX == -3 && quartY == -2 && quartZ == 4 ? desert : plains);
        var validContext = new Structure.GenerationContext(
            world.Generator,
            validResolver,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));
        var validStructure = new ProbeStructure(stub);

        Require(
            ReferenceEquals(validStructure.FindValidGenerationPoint(validContext), stub) &&
            validStructure.FindCalls == 1 &&
            validResolver.Queries.SequenceEqual(new[] { (-3, -2, 4) }),
            "FindValidGenerationPoint must retain a generated stub when its origin biome is accepted.");

        var invalidResolver = new RecordingBiomeResolver((_, _, _) => plains);
        var invalidContext = new Structure.GenerationContext(
            world.Generator,
            invalidResolver,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            biome => ReferenceEquals(biome, desert));
        var invalidStructure = new ProbeStructure(stub);

        Require(
            invalidStructure.FindValidGenerationPoint(invalidContext) is null &&
            invalidStructure.FindCalls == 1 &&
            invalidResolver.Queries.SequenceEqual(new[] { (-3, -2, 4) }),
            "FindValidGenerationPoint must reject a generated stub when its origin biome is invalid.");

        var noStubResolver = new RecordingBiomeResolver((_, _, _) =>
            throw new InvalidOperationException("Biome lookup must not run when no stub is generated."));
        var noStubContext = new Structure.GenerationContext(
            world.Generator,
            noStubResolver,
            world.Generator.RandomState,
            world.Seed,
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 64),
            static _ => true);
        var noStubStructure = new ProbeStructure(null);

        Require(
            noStubStructure.FindValidGenerationPoint(noStubContext) is null &&
            noStubStructure.FindCalls == 1 &&
            noStubResolver.Queries.Count == 0,
            "FindValidGenerationPoint must preserve an empty generation result without querying a biome.");

        RequireThrows<ArgumentNullException>(
            () => validStructure.FindValidGenerationPoint(null!),
            "FindValidGenerationPoint must reject a null context.");
    }

    private static void CheckOnTopOfChunkCenterHelpers()
    {
        const long seed = 0x43454E5445525354L;
        OverworldWorld world = CreateWorld(seed);
        ChunkPos chunkPosition = new(-1, 2);
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        int centerX = chunkPosition.GetMiddleBlockX();
        int centerZ = chunkPosition.GetMiddleBlockZ();
        var pieceGenerator = new Action<StructurePiecesBuilder>(static _ => { });

        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var acceptedResolver = new RecordingBiomeResolver((quartX, quartY, quartZ) =>
            quartX == -2 && quartZ == 10 && quartY == 0 ? desert : plains);
        var acceptedContext = new Structure.GenerationContext(
            world.Generator,
            acceptedResolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            biome => ReferenceEquals(biome, desert));
        var structure = new CenterProbeStructure();

        int expectedY = world.Generator.GetFirstOccupiedHeight(
            centerX,
            centerZ,
            Heightmap.Types.WORLD_SURFACE_WG,
            heightAccessor,
            world.Generator.RandomState);
        Structure.GenerationStub? accepted = structure.BuildOnTopOfChunkCenter(
            acceptedContext, Heightmap.Types.WORLD_SURFACE_WG, pieceGenerator);

        Require(
            accepted is not null &&
            accepted.Position.Equals(new BlockPos(centerX, expectedY, centerZ)) &&
            acceptedResolver.Queries.SequenceEqual(Enumerable.Range(-5, 6).Select(y => (-2, y, 10))),
            "OnTopOfChunkCenter must validate the inclusive quart column, then use the generator's first occupied height at the chunk center.");

        var rejectedResolver = new RecordingBiomeResolver((_, _, _) => plains);
        var rejectedContext = new Structure.GenerationContext(
            world.Generator,
            rejectedResolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            biome => ReferenceEquals(biome, desert));
        Require(
            structure.BuildOnTopOfChunkCenter(
                rejectedContext, Heightmap.Types.WORLD_SURFACE_WG, pieceGenerator) is null &&
            rejectedResolver.Queries.Count == 17,
            "OnTopOfChunkCenter must return no stub when the center column contains no valid biome.");

        var uncheckedResolver = new RecordingBiomeResolver((_, _, _) =>
            throw new InvalidOperationException("Biome lookup must not run in the unchecked helper."));
        var uncheckedContext = new Structure.GenerationContext(
            world.Generator,
            uncheckedResolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            static _ => false);
        Structure.GenerationStub uncheckedStub = structure.BuildOnTopOfChunkCenterWithoutBiomeCheck(
            uncheckedContext, Heightmap.Types.WORLD_SURFACE_WG, pieceGenerator);

        Require(
            uncheckedStub.Position.Equals(new BlockPos(centerX, expectedY, centerZ)) &&
            uncheckedResolver.Queries.Count == 0,
            "The unchecked helper must compute the same center-column height without querying the biome resolver.");

        RequireThrows<ArgumentNullException>(
            () => structure.BuildOnTopOfChunkCenter(null!, Heightmap.Types.WORLD_SURFACE_WG, pieceGenerator),
            "OnTopOfChunkCenter must reject a null generation context.");
        RequireThrows<ArgumentNullException>(
            () => structure.BuildOnTopOfChunkCenter(acceptedContext, null!, pieceGenerator),
            "OnTopOfChunkCenter must reject a null heightmap type.");
        RequireThrows<ArgumentNullException>(
            () => structure.BuildOnTopOfChunkCenter(acceptedContext, Heightmap.Types.WORLD_SURFACE_WG, null!),
            "OnTopOfChunkCenter must reject a null piece generator.");
    }
    private static void CheckStructureStartGenerationFromStub()
    {
        const long seed = 0x5354525543545354L;
        OverworldWorld world = CreateWorld(seed);
        ChunkPos chunkPosition = new(-4, 7);
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        Holder<Biome> plains = Biomes.PLAINS.BuiltInRegistryHolder;
        Holder<Biome> desert = Biomes.DESERT.BuiltInRegistryHolder;
        var validResolver = new RecordingBiomeResolver((_, _, _) => plains);
        var validContext = new Structure.GenerationContext(
            world.Generator,
            validResolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            biome => ReferenceEquals(biome, plains));

        var piece = new TestPiece(new BoundingBox(-64, 20, 112, -55, 29, 121));
        int builderCalls = 0;
        var stub = new Structure.GenerationStub(
            new BlockPos(-56, 30, 120),
            builder =>
            {
                builderCalls++;
                builder.AddPiece(piece);
            });
        var structure = new ProbeStructure(stub);
        const int references = 3;

        StructureStart start = structure.Generate(validContext, references);
        Require(
            start.IsValid() &&
            ReferenceEquals(start.GetStructure(), structure) &&
            start.GetChunkPos() == chunkPosition &&
            start.GetReferences() == references &&
            start.GetPieces().Count == 1 &&
            ReferenceEquals(start.GetPieces()[0], piece) &&
            start.GetBoundingBox().Equals(piece.GetBoundingBox()) &&
            structure.FindCalls == 1 &&
            builderCalls == 1,
            "Generate must validate one stub, execute its deferred builder once and retain the resulting pieces in a valid StructureStart.");

        var rejectedContext = new Structure.GenerationContext(
            world.Generator,
            new RecordingBiomeResolver((_, _, _) => plains),
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            biome => ReferenceEquals(biome, desert));
        var rejectedStructure = new ProbeStructure(stub);
        Require(
            ReferenceEquals(rejectedStructure.Generate(rejectedContext, references), StructureStart.INVALID_START) &&
            rejectedStructure.FindCalls == 1 &&
            builderCalls == 1,
            "Generate must return the invalid sentinel without executing a stub rejected by its biome predicate.");

        var noStubStructure = new ProbeStructure(null);
        Require(
            ReferenceEquals(noStubStructure.Generate(validContext, references), StructureStart.INVALID_START) &&
            noStubStructure.FindCalls == 1 &&
            builderCalls == 1,
            "Generate must return the invalid sentinel when no generation stub is available.");

        int emptyBuilderCalls = 0;
        var emptyStub = new Structure.GenerationStub(
            new BlockPos(0, 0, 0),
            _ => emptyBuilderCalls++);
        var emptyStructure = new ProbeStructure(emptyStub);
        Require(
            ReferenceEquals(emptyStructure.Generate(validContext, references), StructureStart.INVALID_START) &&
            emptyBuilderCalls == 1,
            "Generate must discard a builder that produces no pieces and return the invalid sentinel.");

        RequireThrows<ArgumentNullException>(
            () => structure.Generate(null!, references),
            "Generate must reject a null generation context.");
    }
    private static void CheckStructureHeightSamplingHelpers()
    {
        const long seed = 0x4845494748545354L;
        OverworldWorld world = CreateWorld(seed);
        ChunkPos chunkPosition = new(-3, -2);
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        var resolver = new RecordingBiomeResolver((_, _, _) => Biomes.PLAINS.BuiltInRegistryHolder);
        var context = new Structure.GenerationContext(
            world.Generator,
            resolver,
            world.Generator.RandomState,
            seed,
            chunkPosition,
            heightAccessor,
            static _ => true);
        const int minX = -47;
        const int minZ = -31;
        const int sizeX = 7;
        const int sizeZ = 11;

        int[] expectedCorners =
        [
            world.Generator.GetFirstOccupiedHeight(minX, minZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(minX, minZ + sizeZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(minX + sizeX, minZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(minX + sizeX, minZ + sizeZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState)
        ];
        int expectedMean = unchecked(
            expectedCorners[0] + expectedCorners[1] + expectedCorners[2] + expectedCorners[3]) / 4;
        int expectedLowest = expectedCorners.Min();

        Require(
            Structure.GetMeanFirstOccupiedHeight(context, minX, sizeX, minZ, sizeZ) == expectedMean,
            "GetMeanFirstOccupiedHeight must average the four WORLD_SURFACE_WG corner samples using integer division.");
        var heightStructure = new HeightProbeStructure();
        Require(
            heightStructure.GetLowestYForFootprint(context, minX, minZ, sizeX, sizeZ) == expectedLowest,
            "GetLowestY must return the minimum of the four explicitly positioned WORLD_SURFACE_WG corner samples.");

        int chunkMinX = chunkPosition.GetMinBlockX();
        int chunkMinZ = chunkPosition.GetMinBlockZ();
        int expectedChunkLowest = new[]
        {
            world.Generator.GetFirstOccupiedHeight(chunkMinX, chunkMinZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(chunkMinX, chunkMinZ + sizeZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(chunkMinX + sizeX, chunkMinZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState),
            world.Generator.GetFirstOccupiedHeight(chunkMinX + sizeX, chunkMinZ + sizeZ, Heightmap.Types.WORLD_SURFACE_WG, heightAccessor, world.Generator.RandomState)
        }.Min();
        Require(
            heightStructure.GetLowestYForChunkFootprint(context, sizeX, sizeZ) == expectedChunkLowest,
            "GetLowestY with a chunk footprint must begin at the chunk's minimum block X/Z coordinates.");
        Require(
            resolver.Queries.Count == 0,
            "Structure height sampling must not query the biome resolver.");

        RequireThrows<ArgumentNullException>(
            () => Structure.GetMeanFirstOccupiedHeight(null!, minX, sizeX, minZ, sizeZ),
            "GetMeanFirstOccupiedHeight must reject a null context.");
        RequireThrows<ArgumentNullException>(
            () => heightStructure.GetLowestYForFootprint(null!, minX, minZ, sizeX, sizeZ),
            "GetLowestY must reject a null context.");
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

    private sealed class ProbeStructure(Structure.GenerationStub? stub)
        : Structure(StructureSettings.DEFAULT)
    {
        public int FindCalls { get; private set; }

        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context)
        {
            FindCalls++;
            return stub;
        }
    }

    private sealed class CenterProbeStructure : Structure
    private sealed class TestPiece(BoundingBox boundingBox)
        : StructurePiece(0, boundingBox);

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
