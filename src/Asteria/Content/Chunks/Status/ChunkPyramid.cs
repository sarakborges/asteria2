using Asteria.Core;
using Asteria.World.LevelGen;

namespace Asteria.Content.Chunks.Status;

/// <summary>
/// Immutable loading or generation dependency pyramid for Asteria's mob-free chunk stages.
/// The vanilla SPAWN stage is omitted because it performs mob spawning.
/// Only stages with implemented local task semantics have delegates; all others fail closed.
/// </summary>
public sealed class ChunkPyramid
{
    public static readonly ChunkPyramid GenerationPyramid =
        new Builder()
            .Step(ChunkStatus.EMPTY, static builder => builder)
            .Step(
                ChunkStatus.STRUCTURE_STARTS,
                static builder => builder.SetTask(ChunkStatusTasks.GenerateStructureStarts))
            .Step(
                ChunkStatus.STRUCTURE_REFERENCES,
                static builder => builder
                    .AddRequirement(ChunkStatus.STRUCTURE_STARTS, ChunkStatus.MAX_STRUCTURE_DISTANCE)
                    .SetTask(ChunkStatusTasks.GenerateStructureReferences))
            .Step(
                ChunkStatus.BIOMES,
                static builder => builder
                    .AddRequirement(ChunkStatus.STRUCTURE_STARTS, 8)
                    .SetTask(ChunkStatusTasks.GenerateBiomes))
            .Step(
                ChunkStatus.TERRAIN,
                static builder => builder
                    .AddRequirement(ChunkStatus.STRUCTURE_STARTS, 8)
                    .AddRequirement(ChunkStatus.BIOMES, 1)
                    .BlockStateWriteRadius(0)
                    .SetTask(ChunkStatusTasks.GenerateTerrain))
            .Step(
                ChunkStatus.FEATURES,
                static builder => builder
                    .AddRequirement(ChunkStatus.STRUCTURE_STARTS, 8)
                    .AddRequirement(ChunkStatus.TERRAIN, 1)
                    .BlockStateWriteRadius(1)
                    .SetTask(ChunkStatusTasks.GenerateFeatures))
            .Step(ChunkStatus.INITIALIZE_LIGHT, static builder => builder)
            .Step(
                ChunkStatus.LIGHT,
                static builder => builder.AddRequirement(ChunkStatus.INITIALIZE_LIGHT, 1))
            .Step(ChunkStatus.FULL, static builder => builder)
            .Build();

    public static readonly ChunkPyramid LoadingPyramid =
        new Builder()
            .Step(ChunkStatus.EMPTY, static builder => builder)
            .Step(ChunkStatus.STRUCTURE_STARTS, static builder => builder)
            .Step(ChunkStatus.STRUCTURE_REFERENCES, static builder => builder)
            .Step(ChunkStatus.BIOMES, static builder => builder)
            .Step(ChunkStatus.TERRAIN, static builder => builder)
            .Step(ChunkStatus.FEATURES, static builder => builder)
            .Step(ChunkStatus.INITIALIZE_LIGHT, static builder => builder)
            .Step(
                ChunkStatus.LIGHT,
                static builder => builder.AddRequirement(ChunkStatus.INITIALIZE_LIGHT, 1))
            .Step(ChunkStatus.FULL, static builder => builder)
            .Build();

    private static readonly int SAFETY_MARGIN_CHUNKS =
        unchecked((32 + GenerationPyramid.GetStepTo(ChunkStatus.FULL)
            .AccumulatedDependencies.Size() + 1) * 2);

    public static readonly int MaxChunkCoordinateValue =
        unchecked(SectionPos.BlockToSectionCoord(BlockPos.MaxHorizontalCoordinate) -
            SAFETY_MARGIN_CHUNKS);

    private readonly IReadOnlyList<ChunkStep> _steps;

    private ChunkPyramid(IEnumerable<ChunkStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = Array.AsReadOnly(steps.ToArray());
    }

    public IReadOnlyList<ChunkStep> Steps => _steps;

    public ChunkStep GetStepTo(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return _steps[status.GetIndex()];
    }

    public sealed class Builder
    {
        private readonly List<ChunkStep> _steps = new();

        public Builder Step(
            ChunkStatus status,
            Func<ChunkStep.Builder, ChunkStep.Builder> configure)
        {
            ArgumentNullException.ThrowIfNull(status);
            ArgumentNullException.ThrowIfNull(configure);

            ChunkStep.Builder stepBuilder = _steps.Count == 0
                ? new ChunkStep.Builder(status)
                : new ChunkStep.Builder(status, _steps[^1]);

            ChunkStep.Builder configured = configure(stepBuilder) ??
                throw new InvalidOperationException("A chunk step configuration cannot return null.");
            _steps.Add(configured.Build());
            return this;
        }

        public ChunkPyramid Build() => new(_steps);
    }
}
