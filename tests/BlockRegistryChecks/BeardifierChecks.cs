using System.Text.Json;
using Asteria.Core;
using Asteria.Content.Biomes;
using BuiltInBiomes = Asteria.Content.Biomes.Biomes;
using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;
using Asteria.World.LevelGen.Structure.Pieces;
using Asteria.Serialization;
using Asteria.Util.Context;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pools;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class BeardifierChecks
{
    public static void Run()
    {
        CheckTerrainAdjustmentCodec();
        CheckContextBinding();
        CheckMathPrimitives();
        CheckPointSampling();
        CheckJunctionSampling();
        CheckKernelBounds();
        CheckVolumeSampling();
        CheckStructureMetadataCollection();
    }

    private static void CheckContextBinding()
    {
        Require(
            ReferenceEquals(Beardifier.CONTEXT_KEY, DensityFunctionContextKeys.BEARDIFIER),
            "Density-function and Beardifier context access must share the exact vanilla ContextKey instance.");

        DensitySampler sampler = SimpleDensityFunction.BEARDIFIER.CompileSampler(null!);
        float fallback = sampler.SampleValue(
            SamplerContext.EmptyUncached,
            0, 0, 0);
        Require(
            fallback == 0.0F,
            "The beardifier density function must fall back to Beardifier.EMPTY when the context field is absent.");

        var pieceBox = new BoundingBox(0, 0, 0, 0, 0, 0);
        var beardifier = CreateSingleRigid(
            TerrainAdjustment.BURY,
            pieceBox,
            pieceBox);
        ContextMap userFields = ContextMap.CreateBuilder()
            .WithParameter(Beardifier.CONTEXT_KEY, beardifier)
            .Build();
        SamplerContext context = SamplerContext.CreateBuilder()
            .SetUserFields(userFields)
            .Build();

        Require(
            sampler.SampleValue(context, 0, 0, 0) == 1.0F,
            "The beardifier density function must sample the exact Beardifier instance injected into the context.");
    }

    private static void CheckTerrainAdjustmentCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        foreach (TerrainAdjustment adjustment in TerrainAdjustment.Values)
        {
            JsonElement encoded = TerrainAdjustment.CODEC
                .EncodeStart(ops, adjustment)
                .GetOrThrow();

            Require(
                ops.GetStringValue(encoded).GetOrThrow() == adjustment.GetSerializedName(),
                "TerrainAdjustment must serialize using its exact lowercase identifier.");
            Require(
                ReferenceEquals(
                    TerrainAdjustment.CODEC.Parse(ops, encoded).GetOrThrow(),
                    adjustment),
                "TerrainAdjustment decoding must resolve the canonical singleton.");
        }

        Require(
            TerrainAdjustment.CODEC.Parse(
                ops,
                ops.CreateString("beard_thick")).IsError,
            "TerrainAdjustment must reject identifiers that are not defined in 26.3.");
    }

    private static void CheckMathPrimitives()
    {
        Require(Mth.Sqrt(9.0F) == 3.0F,
            "Mth.sqrt(float) must return the float conversion of Math.sqrt.");
        Require(Mth.LengthSquared(1.0F, 2.0F, 3.0F) == 14.0F,
            "Mth.lengthSquared(float,float,float) must preserve float arithmetic.");
        Require(
            Math.Abs(Mth.FastInvSqrt(0.125) - 2.8277186031818555) < 1.0E-15,
            "Mth.fastInvSqrt must preserve the 64-bit inverse-square-root approximation.");
    }

    private static void CheckPointSampling()
    {
        BoundingBox pieceBox = new(0, 0, 0, 0, 0, 0);
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);

        Beardifier bury = CreateSingleRigid(TerrainAdjustment.BURY, pieceBox, affectedBox);
        Require(bury.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 1.0F,
            "BURY must contribute its full value at the rigid piece's ground origin.");
        Require(Math.Abs(bury.SampleValue(SamplerContext.EmptyUncached, 3, 0, 0) - 0.5F) < 1.0E-6F,
            "BURY must use the six-block radial falloff and halve vertical distance.");
        Require(Math.Abs(bury.SampleValue(SamplerContext.EmptyUncached, 0, 6, 0) - 0.5F) < 1.0E-6F,
            "BURY must halve the vertical distance before evaluating its radial falloff.");
        Require(bury.SampleValue(SamplerContext.EmptyUncached, 6, 0, 0) == 0.0F,
            "BURY must reach zero at the inclusive six-block distance threshold.");

        Beardifier none = CreateSingleRigid(TerrainAdjustment.NONE, pieceBox, affectedBox);
        Require(none.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 0.0F,
            "NONE must not deform terrain.");

        Beardifier encapsulate =
            CreateSingleRigid(TerrainAdjustment.ENCAPSULATE, pieceBox, affectedBox);
        Require(Math.Abs(encapsulate.SampleValue(SamplerContext.EmptyUncached, 6, 0, 0) - 0.4F) < 1.0E-6F,
            "ENCAPSULATE must halve all axes and apply the 0.8 contribution factor.");

        Beardifier beardThin =
            CreateSingleRigid(TerrainAdjustment.BEARD_THIN, pieceBox, affectedBox);
        Require(Math.Abs(beardThin.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.5567757487297058F) < 1.0E-6F,
            "BEARD_THIN must use the precomputed kernel, inverse-square-root factor and 0.8 scale.");

        Beardifier beardBox =
            CreateSingleRigid(TerrainAdjustment.BEARD_BOX, pieceBox, affectedBox);
        Require(Math.Abs(beardBox.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.5567757487297058F) < 1.0E-6F,
            "BEARD_BOX must use its ground-to-box vertical distance and the beard kernel.");
        Require(
            bury.SampleValue(SamplerContext.EmptyUncached, 25, 0, 0) == 0.0F,
            "Point sampling must return zero outside the precomputed affected box.");

        Beardifier twoPieces = new(
            [
                new Beardifier.Rigid(pieceBox, TerrainAdjustment.BURY, 0),
                new Beardifier.Rigid(pieceBox, TerrainAdjustment.BURY, 0)
            ],
            Array.Empty<JigsawJunction>(),
            affectedBox);
        Require(twoPieces.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 2.0F,
            "Rigid-piece contributions must accumulate in source list order.");
    }

    private static void CheckJunctionSampling()
    {
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);
        var junction = new JigsawJunction(0, 0, 0, 0, StructureTemplatePool.Projection.RIGID);
        var beardifier = new Beardifier(
            Array.Empty<Beardifier.Rigid>(),
            [junction],
            affectedBox);

        Require(Math.Abs(beardifier.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) -
                         -0.2783878743648529F) < 1.0E-6F,
            "Each junction must add the beard-kernel contribution scaled by 0.4.");
    }

    private static void CheckKernelBounds()
    {
        BoundingBox affectedBox = new(-24, -24, -24, 24, 24, 24);
        var junction = new JigsawJunction(-12, 0, 0, 0, StructureTemplatePool.Projection.RIGID);
        var beardifier = new Beardifier(
            Array.Empty<Beardifier.Rigid>(),
            [junction],
            affectedBox);

        Require(
            beardifier.SampleValue(SamplerContext.EmptyUncached, 12, 0, 0) == 0.0F,
            "The beard kernel must return zero when any shifted coordinate reaches 24.");
    }

    private static void CheckVolumeSampling()
    {
        BoundingBox pieceBox = new(0, 0, 0, 0, 0, 0);
        var beardifier = CreateSingleRigid(
            TerrainAdjustment.BURY,
            pieceBox,
            pieceBox);
        var volume = new DensityVolume(
            3, 3, 3,
            -2, -2, -2,
            2, 2, 2);
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());
        buffer.Fill(-1.0F);

        beardifier.SampleVolume(SamplerContext.EmptyUncached, buffer, volume);

        int originIndex = volume.IndexUnchecked(1, 1, 1);
        for (int index = 0; index < buffer.Size(); index++)
        {
            float expected = index == originIndex ? 1.0F : 0.0F;
            Require(buffer.Get(index) == expected,
                "Volume sampling must zero the full output and write only sampled points inside the affected box.");
        }

        DensityBuffer emptyBuffer = DensityBuffer.CreateUnpooled(volume.Size());
        emptyBuffer.Fill(7.0F);
        Beardifier.EMPTY.SampleVolume(SamplerContext.EmptyUncached, emptyBuffer, volume);
        for (int index = 0; index < emptyBuffer.Size(); index++)
        {
            Require(emptyBuffer.Get(index) == 0.0F,
                "The empty Beardifier must clear the output volume to zero.");
        }
    }

    private static void CheckStructureMetadataCollection()
    {
        const long seed = 0x4245415244464945L;
        LevelHeightAccessor heightAccessor = LevelHeightAccessor.Create(-16, 64);
        var world = new OverworldWorld(seed, heightAccessor);
        var context = new WorldGenContext(world, world.Generator, world.LightEngine);
        ChunkPos centerPosition = ChunkPos.Zero;
        ChunkStep terrainStep = ChunkPyramid.GenerationPyramid.GetStepTo(ChunkStatus.TERRAIN);
        var chunks = new Dictionary<ChunkPos, ProtoChunk>();

        foreach (ChunkPos position in ChunkPos.RangeClosed(centerPosition, ChunkStatus.MAX_STRUCTURE_DISTANCE))
        {
            var chunk = new ProtoChunk(position, heightAccessor, PalettedContainerFactory.CreateBuiltIn());
            ChunkStatus requiredStatus = terrainStep.DirectDependencies.Get(
                centerPosition.GetChessboardDistance(position));
            chunk.SetPersistedStatus(requiredStatus);
            chunks.Add(position, chunk);
        }

        ProtoChunk center = chunks[centerPosition];
        ChunkPos rigidSource = new(2, -3);
        var rigidStructure = new TerrainProbeStructure(TerrainAdjustment.BURY);
        var rigidPiece = new TerrainProbePiece(
            new BoundingBox(0, 0, 0, 0, 0, 0),
            isRigid: true,
            groundLevelDelta: 2);
        chunks[rigidSource].SetStartForStructure(
            rigidStructure,
            new StructureStart(
                rigidStructure,
                rigidSource,
                0,
                new PiecesContainer(new[] { rigidPiece })));
        center.AddReferenceForStructure(rigidStructure, rigidSource.Pack());

        ChunkPos junctionSource = new(-4, 5);
        var junctionStructure = new TerrainProbeStructure(TerrainAdjustment.BURY);
        var junctionPiece = new TerrainProbePiece(
            new BoundingBox(40, 0, 40, 40, 0, 40),
            isRigid: false,
            junctions: new[]
            {
                new JigsawJunction(0, 0, 0, 0, StructureTemplatePool.Projection.RIGID)
            });
        chunks[junctionSource].SetStartForStructure(
            junctionStructure,
            new StructureStart(
                junctionStructure,
                junctionSource,
                0,
                new PiecesContainer(new[] { junctionPiece })));
        center.AddReferenceForStructure(junctionStructure, junctionSource.Pack());

        ChunkPos ignoredSource = new(1, 1);
        var ignoredStructure = new TerrainProbeStructure(TerrainAdjustment.NONE);
        chunks[ignoredSource].SetStartForStructure(
            ignoredStructure,
            new StructureStart(
                ignoredStructure,
                ignoredSource,
                0,
                new PiecesContainer(new[]
                {
                    new TerrainProbePiece(new BoundingBox(100, 0, 100, 100, 0, 100), isRigid: true, groundLevelDelta: 0)
                })));
        center.AddReferenceForStructure(ignoredStructure, ignoredSource.Pack());

        IReadOnlyList<ProtoChunk> availableChunks = chunks.Values.ToArray();
        WorldGenRegion region = world.CreateWorldGenRegion(terrainStep, center, availableChunks);
        ChunkAccess recoveredRigidChunk = region.GetChunk(
            rigidSource.X, rigidSource.Z, ChunkStatus.STRUCTURE_STARTS);
        StructureStart? recoveredRigidStart =
            recoveredRigidChunk.GetStartForStructure(rigidStructure);
        float recoveredRigidContribution = rigidPiece.IsRigidForTerrain
            ? CreateSingleRigid(
                rigidStructure.TerrainAdaptation(),
                rigidPiece.GetBoundingBox(),
                rigidStructure.AdjustBoundingBox(rigidPiece.GetBoundingBox()))
                .SampleValue(SamplerContext.EmptyUncached, 0, 2, 0)
            : float.NaN;
        Beardifier beardifier = Beardifier.CreateForChunk(region, center);
        float collectedRigidContribution =
            beardifier.SampleValue(SamplerContext.EmptyUncached, 0, 2, 0);

        Require(
            center.GetReferencesForStructure(rigidStructure).Contains(rigidSource.Pack()) &&
            recoveredRigidStart is not null &&
            recoveredRigidStart.IsValid() &&
            ReferenceEquals(recoveredRigidStart.GetPieces()[0], rigidPiece),
            "The Beardifier fixture must resolve the referenced source chunk and its exact valid rigid start.");
        Require(
            rigidPiece.IsRigidForTerrain &&
            rigidPiece.GetGroundLevelDelta() == 2 &&
            Math.Abs(recoveredRigidContribution - 1.0F) < 1.0E-6F,
            "The hand-built test piece must expose its expected rigid projection and ground-level delta.");
        Require(
            Math.Abs(collectedRigidContribution - 1.0F) < 1.0E-6F,
            $"Hand-built rigid pieces must contribute with their ground-level delta; collected density was {collectedRigidContribution}.");
        Require(
            Math.Abs(beardifier.SampleValue(SamplerContext.EmptyUncached, 40, 0, 40) -
                     -0.2783878743648529F) < 1.0E-6F,
            "Jigsaw junctions must contribute independently when their source piece is not rigid.");
        Require(
            beardifier.SampleValue(SamplerContext.EmptyUncached, 100, 0, 100) == 0.0F,
            "Structure starts with TerrainAdjustment.NONE must not expand the beardifier's affected bounds or contribute density.");

        var emptyCenter = new ProtoChunk(
            centerPosition, heightAccessor, PalettedContainerFactory.CreateBuiltIn());
        emptyCenter.SetPersistedStatus(ChunkStatus.BIOMES);
        WorldGenRegion emptyRegion = world.CreateWorldGenRegion(
            terrainStep, emptyCenter, new[] { emptyCenter });
        Require(
            ReferenceEquals(Beardifier.CreateForChunk(emptyRegion, emptyCenter), Beardifier.EMPTY),
            "Chunks without structure references must reuse Beardifier.EMPTY.");
    }
    private static Beardifier CreateSingleRigid(
        TerrainAdjustment adjustment,
        BoundingBox pieceBox,
        BoundingBox affectedBox) =>
        new(
            [new Beardifier.Rigid(pieceBox, adjustment, 0)],
            Array.Empty<JigsawJunction>(),
            affectedBox);

    private sealed class TerrainProbeStructure(TerrainAdjustment adjustment)
        : Structure(new StructureSettings(
            HolderSet<Biome>.Direct(BuiltInBiomes.PLAINS.BuiltInRegistryHolder),
            GenerationStep.Decoration.SURFACE_STRUCTURES,
            adjustment))
    {
        protected override Structure.GenerationStub? FindGenerationPoint(
            Structure.GenerationContext context) => null;
    }

    private sealed class TerrainProbePiece : StructurePiece
    {
        private readonly bool _isRigid;
        private readonly int _groundLevelDelta;
        private readonly IReadOnlyList<JigsawJunction> _junctions;

        public TerrainProbePiece(
            BoundingBox box,
            bool isRigid,
            int groundLevelDelta = 0,
            IReadOnlyList<JigsawJunction>? junctions = null)
            : base(0, box)
        {
            _isRigid = isRigid;
            _groundLevelDelta = groundLevelDelta;
            _junctions = junctions ?? Array.Empty<JigsawJunction>();
        }

        public override bool IsRigidForTerrain => _isRigid;
        public override int GetGroundLevelDelta() => _groundLevelDelta;
        public override IReadOnlyList<JigsawJunction> GetJunctions() => _junctions;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
