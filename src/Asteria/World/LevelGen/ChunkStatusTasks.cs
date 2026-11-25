using Asteria.Content.Chunks;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Content.Chunks.Status;
using Asteria.Util;
using Asteria.World.Level;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pieces;
using StructureType = Asteria.World.LevelGen.Structure.Structure;

namespace Asteria.World.LevelGen;

/// <summary>
/// Status-task implementations whose required world-generation dependencies are
/// currently available in Asteria. Unported tasks remain absent from ChunkPyramid
/// and therefore fail closed when requested.
/// </summary>
public static class ChunkStatusTasks
{
    /// <summary>
    /// Port of Minecraft Java 26.3 generateBiomes: populate the target proto chunk's
    /// quart biome palette through the configured chunk generator.
    /// </summary>
    public static ValueTask<ChunkAccess> GenerateBiomes(
        WorldGenContext context,
        ChunkStep step,
        StaticCache2D<ChunkAccess?> chunks,
        ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!ReferenceEquals(step.TargetStatus, ChunkStatus.BIOMES))
        {
            throw new ArgumentException(
                $"The biome task cannot execute status {step.TargetStatus}.",
                nameof(step));
        }

        ChunkPos position = chunk.GetPos();
        if (!chunks.Contains(position.X, position.Z) ||
            !ReferenceEquals(chunks.Get(position.X, position.Z), chunk))
        {
            throw new InvalidOperationException(
                $"The BIOMES task cache does not contain its center chunk {position}.");
        }

        if (chunk is not ProtoChunk protoChunk)
        {
            throw new InvalidOperationException(
                $"BIOMES generation requires a ProtoChunk, received {chunk.GetType().Name}.");
        }

        context.Generator.CreateBiomes(protoChunk);
        return ValueTask.FromResult<ChunkAccess>(protoChunk);
    }

    /// <summary>
    /// Generates the center chunk's starts from the dimension's registered StructureSets.
    /// Placement owns candidate/frequency/exclusion checks; the generator owns weighted
    /// structure choice and candidate-specific biome validation.
    /// </summary>
    public static ValueTask<ChunkAccess> GenerateStructureStarts(
        WorldGenContext context,
        ChunkStep step,
        StaticCache2D<ChunkAccess?> chunks,
        ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!ReferenceEquals(step.TargetStatus, ChunkStatus.STRUCTURE_STARTS))
        {
            throw new ArgumentException(
                $"The structure-start task cannot execute status {step.TargetStatus}.",
                nameof(step));
        }

        ChunkPos position = chunk.GetPos();
        if (!chunks.Contains(position.X, position.Z) ||
            !ReferenceEquals(chunks.Get(position.X, position.Z), chunk))
        {
            throw new InvalidOperationException(
                $"The STRUCTURE_STARTS task cache does not contain its center chunk {position}.");
        }

        if (chunk is not ProtoChunk protoChunk)
        {
            throw new InvalidOperationException(
                $"STRUCTURE_STARTS generation requires a ProtoChunk, received {chunk.GetType().Name}.");
        }

        if (context.StructureState.PossibleStructureSets().Count == 0)
            return ValueTask.FromResult<ChunkAccess>(protoChunk);

        BiomeResolver biomeResolver = context.Generator.CreateUncachedBiomeResolver();
        var heightAccessor = LevelHeightAccessor.Create(
            protoChunk.GetMinY(),
            protoChunk.GetHeight());

        var generationContext = new StructureType.GenerationContext(
            context.Generator,
            biomeResolver,
            context.StructureState.RandomState(),
            context.StructureState.LevelSeed,
            position,
            heightAccessor,
            static _ => true);

        foreach (Holder<StructureSet> structureSetHolder in
                 context.StructureState.PossibleStructureSets())
        {
            StructureSet structureSet = structureSetHolder.Value;
            if (!structureSet.Placement.IsStructureChunk(
                    context.StructureState,
                    position.X,
                    position.Z))
            {
                continue;
            }

            StructureStart? start = context.Generator.TryGenerateStructure(
                structureSet,
                generationContext,
                references: 0);
            if (start is null || !start.IsValid())
                continue;

            StructureType structure = start.GetStructure() ??
                throw new InvalidOperationException(
                    "A valid structure start must retain its structure definition.");
            protoChunk.SetStartForStructure(structure, start);
        }

        return ValueTask.FromResult<ChunkAccess>(protoChunk);
    }

    /// <summary>
    /// Finds structure starts in the 8-chunk neighborhood whose XZ bounds intersect
    /// the center chunk, then stores each source chunk position on the center chunk.
    /// </summary>
    public static ValueTask<ChunkAccess> GenerateStructureReferences(
        WorldGenContext context,
        ChunkStep step,
        StaticCache2D<ChunkAccess?> chunks,
        ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!ReferenceEquals(step.TargetStatus, ChunkStatus.STRUCTURE_REFERENCES))
        {
            throw new ArgumentException(
                $"The structure-reference task cannot execute status {step.TargetStatus}.",
                nameof(step));
        }

        ChunkPos centerPosition = chunk.GetPos();
        if (!chunks.Contains(centerPosition.X, centerPosition.Z) ||
            !ReferenceEquals(chunks.Get(centerPosition.X, centerPosition.Z), chunk))
        {
            throw new InvalidOperationException(
                $"The STRUCTURE_REFERENCES task cache does not contain its center chunk {centerPosition}.");
        }

        if (!chunk.GetPersistedStatus().IsOrAfter(ChunkStatus.STRUCTURE_STARTS))
        {
            throw new InvalidOperationException(
                $"The center chunk {centerPosition} has not reached STRUCTURE_STARTS.");
        }

        WorldGenRegion region = context.CreateWorldGenRegion(step, chunk, chunks);
        int minBlockX = centerPosition.GetMinBlockX();
        int minBlockZ = centerPosition.GetMinBlockZ();
        int maxBlockX = centerPosition.GetMaxBlockX();
        int maxBlockZ = centerPosition.GetMaxBlockZ();

        foreach (ChunkPos sourcePosition in
                 ChunkPos.RangeClosed(centerPosition, ChunkStatus.MAX_STRUCTURE_DISTANCE))
        {
            ChunkAccess sourceChunk = region.GetChunk(
                sourcePosition.X,
                sourcePosition.Z,
                ChunkStatus.STRUCTURE_STARTS);

            foreach (KeyValuePair<StructureType, StructureStart> entry in
                     sourceChunk.GetAllStarts())
            {
                StructureStart start = entry.Value;
                if (!start.IsValid())
                    continue;

                if (start.GetBoundingBox().Intersects(
                        minBlockX,
                        minBlockZ,
                        maxBlockX,
                        maxBlockZ))
                {
                    chunk.AddReferenceForStructure(entry.Key, sourcePosition.Pack());
                }
            }
        }

        return ValueTask.FromResult(chunk);
    }

    /// <summary>
    /// Builds center-chunk terrain using the Beardifier assembled from its generated
    /// structure references and the required STRUCTURE_STARTS neighborhood.
    /// </summary>
    public static ValueTask<ChunkAccess> GenerateTerrain(
        WorldGenContext context,
        ChunkStep step,
        StaticCache2D<ChunkAccess?> chunks,
        ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!ReferenceEquals(step.TargetStatus, ChunkStatus.TERRAIN))
        {
            throw new ArgumentException(
                $"The terrain task cannot execute status {step.TargetStatus}.",
                nameof(step));
        }

        ChunkPos position = chunk.GetPos();
        if (!chunks.Contains(position.X, position.Z) ||
            !ReferenceEquals(chunks.Get(position.X, position.Z), chunk))
        {
            throw new InvalidOperationException(
                $"The TERRAIN task cache does not contain its exact center chunk {position}.");
        }

        if (chunk is not ProtoChunk protoChunk)
        {
            throw new InvalidOperationException(
                $"TERRAIN generation requires a ProtoChunk, received {chunk.GetType().Name}.");
        }

        if (!protoChunk.GetPersistedStatus().IsOrAfter(ChunkStatus.BIOMES))
        {
            throw new InvalidOperationException(
                $"The center chunk {position} has not reached BIOMES.");
        }

        WorldGenRegion region = context.CreateWorldGenRegion(step, protoChunk, chunks);
        Beardifier beardifier = Beardifier.CreateForChunk(region, protoChunk);
        context.Generator.BuildTerrain(protoChunk, beardifier);

        return ValueTask.FromResult<ChunkAccess>(protoChunk);
    }
}
