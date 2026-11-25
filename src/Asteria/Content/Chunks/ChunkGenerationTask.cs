using Asteria.Content.Chunks.Status;
using Asteria.Util;
using Asteria.World;
using Asteria.World.Level;

namespace Asteria.Content.Chunks;

/// <summary>
/// Schedules the status layers needed to bring one center chunk to a requested target.
/// Port of Minecraft Java 26.3 ChunkGenerationTask.
/// </summary>
public sealed class ChunkGenerationTask
{
    private static readonly ChunkResult<ChunkAccess> NotDoneYet =
        ChunkResult<ChunkAccess>.Error("Not done yet");

    private readonly GeneratingChunkMap _chunkMap;
    private readonly ChunkPos _position;
    private readonly StaticCache2D<GenerationChunkHolder> _cache;
    private readonly List<Task<ChunkResult<ChunkAccess>>> _scheduledLayer = new();

    private ChunkStatus? _scheduledStatus;
    private volatile bool _markedForCancellation;
    private bool _needsGeneration;

    public ChunkStatus TargetStatus { get; }

    private ChunkGenerationTask(
        GeneratingChunkMap chunkMap,
        ChunkStatus targetStatus,
        ChunkPos position,
        StaticCache2D<GenerationChunkHolder> cache)
    {
        _chunkMap = chunkMap;
        TargetStatus = targetStatus;
        _position = position;
        _cache = cache;
    }

    /// <summary>
    /// Acquires every holder in the target status's worst-case generation radius.
    /// The map is responsible for incrementing each holder's generation reference count.
    /// </summary>
    public static ChunkGenerationTask Create(
        GeneratingChunkMap chunkMap,
        ChunkStatus targetStatus,
        ChunkPos position)
    {
        ArgumentNullException.ThrowIfNull(chunkMap);
        ArgumentNullException.ThrowIfNull(targetStatus);

        int worstCaseRadius = ChunkPyramid.GenerationPyramid
            .GetStepTo(targetStatus)
            .GetAccumulatedRadiusOf(ChunkStatus.EMPTY);

        StaticCache2D<GenerationChunkHolder> cache =
            StaticCache2D<GenerationChunkHolder>.Create(
                position.X,
                position.Z,
                worstCaseRadius,
                (x, z) => chunkMap.AcquireGeneration(ChunkPos.Pack(x, z)));

        return new ChunkGenerationTask(chunkMap, targetStatus, position, cache);
    }

    /// <summary>
    /// Continues scheduling layers until one layer has an incomplete future or the
    /// target/cancellation condition is reached. A non-null task is the next wait point.
    /// </summary>
    public Task? RunUntilWait()
    {
        while (true)
        {
            Task? waitingFor = WaitForScheduledLayer();
            if (waitingFor is not null)
                return waitingFor;

            if (_markedForCancellation ||
                ReferenceEquals(_scheduledStatus, TargetStatus))
            {
                ReleaseClaim();
                return null;
            }

            ScheduleNextLayer();
        }
    }

    public void MarkForCancellation() =>
        _markedForCancellation = true;

    public GenerationChunkHolder GetCenter() =>
        _cache.Get(_position.X, _position.Z);

    private void ScheduleNextLayer()
    {
        ChunkStatus statusToSchedule;

        if (_scheduledStatus is null)
        {
            statusToSchedule = ChunkStatus.EMPTY;
        }
        else if (!_needsGeneration &&
                 ReferenceEquals(_scheduledStatus, ChunkStatus.EMPTY) &&
                 !CanLoadWithoutGeneration())
        {
            _needsGeneration = true;
            statusToSchedule = ChunkStatus.EMPTY;
        }
        else
        {
            statusToSchedule = ChunkStatus.GetStatusList()[
                _scheduledStatus.GetIndex() + 1];
        }

        ScheduleLayer(statusToSchedule, _needsGeneration);
        _scheduledStatus = statusToSchedule;
    }

    private void ReleaseClaim()
    {
        GenerationChunkHolder center = GetCenter();
        center.RemoveTask(this);
        _cache.ForEach(_chunkMap.ReleaseGeneration);
    }

    private bool CanLoadWithoutGeneration()
    {
        if (ReferenceEquals(TargetStatus, ChunkStatus.EMPTY))
            return true;

        ChunkStatus? highestGeneratedStatus = GetCenter().GetPersistedStatus();
        if (highestGeneratedStatus is null ||
            highestGeneratedStatus.IsBefore(TargetStatus))
        {
            return false;
        }

        ChunkDependencies dependencies = ChunkPyramid.LoadingPyramid
            .GetStepTo(TargetStatus)
            .AccumulatedDependencies;
        int range = dependencies.GetRadius();

        for (int x = _position.X - range; x <= _position.X + range; x++)
        {
            for (int z = _position.Z - range; z <= _position.Z + range; z++)
            {
                int distance = _position.GetChessboardDistance(x, z);
                ChunkStatus requiredStatus = dependencies.Get(distance);
                ChunkStatus? persistedStatus = _cache.Get(x, z).GetPersistedStatus();

                if (persistedStatus is null ||
                    persistedStatus.IsBefore(requiredStatus))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ScheduleLayer(ChunkStatus status, bool needsGeneration)
    {
        int radius = GetRadiusForLayer(status, needsGeneration);

        for (int x = _position.X - radius; x <= _position.X + radius; x++)
        {
            for (int z = _position.Z - radius; z <= _position.Z + radius; z++)
            {
                GenerationChunkHolder chunkHolder = _cache.Get(x, z);
                if (_markedForCancellation ||
                    !ScheduleChunkInLayer(status, needsGeneration, chunkHolder))
                {
                    return;
                }
            }
        }
    }

    private int GetRadiusForLayer(ChunkStatus status, bool needsGeneration)
    {
        ChunkPyramid pyramid = needsGeneration
            ? ChunkPyramid.GenerationPyramid
            : ChunkPyramid.LoadingPyramid;

        return pyramid.GetStepTo(TargetStatus)
            .GetAccumulatedRadiusOf(status);
    }

    private bool ScheduleChunkInLayer(
        ChunkStatus status,
        bool needsGeneration,
        GenerationChunkHolder chunkHolder)
    {
        ChunkStatus? persistedStatus = chunkHolder.GetPersistedStatus();
        bool generate = persistedStatus is not null &&
            status.IsAfter(persistedStatus);

        ChunkPyramid pyramid = generate
            ? ChunkPyramid.GenerationPyramid
            : ChunkPyramid.LoadingPyramid;

        if (generate && !needsGeneration)
        {
            throw new InvalidOperationException(
                "Can't load chunk, but didn't expect to need to generate");
        }

        Task<ChunkResult<ChunkAccess>> future = chunkHolder.ApplyStep(
            pyramid.GetStepTo(status),
            _chunkMap,
            _cache);

        ChunkResult<ChunkAccess> resultNow = GetNow(future);
        if (ReferenceEquals(resultNow, NotDoneYet))
        {
            _scheduledLayer.Add(future);
            return true;
        }

        if (resultNow.IsSuccess())
            return true;

        MarkForCancellation();
        return false;
    }

    private Task? WaitForScheduledLayer()
    {
        while (_scheduledLayer.Count > 0)
        {
            int lastIndex = _scheduledLayer.Count - 1;
            Task<ChunkResult<ChunkAccess>> lastFuture = _scheduledLayer[lastIndex];
            ChunkResult<ChunkAccess> resultNow = GetNow(lastFuture);

            if (ReferenceEquals(resultNow, NotDoneYet))
                return lastFuture;

            _scheduledLayer.RemoveAt(lastIndex);
            if (!resultNow.IsSuccess())
                MarkForCancellation();
        }

        return null;
    }

    private static ChunkResult<ChunkAccess> GetNow(
        Task<ChunkResult<ChunkAccess>> future) =>
        future.IsCompleted
            ? future.GetAwaiter().GetResult()
            : NotDoneYet;
}
