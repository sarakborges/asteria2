using System.Threading;
using Asteria.Content.Chunks.Status;
using Asteria.World;
using Asteria.World.Level;
using Asteria.Util;

namespace Asteria.Content.Chunks;

/// <summary>
/// Holds the per-status generation futures and atomic work claims for one chunk.
/// This is the stateful core of Minecraft Java 26.3 GenerationChunkHolder; ticket
/// evaluation, task scheduling/cancellation and chunk-map integration are separate ports.
/// </summary>
public abstract class GenerationChunkHolder
{
    private static readonly IReadOnlyList<ChunkStatus> ChunkStatuses = ChunkStatus.GetStatusList();

    private static readonly ChunkResult<ChunkAccess> NotDoneYet =
        ChunkResult<ChunkAccess>.Error("Not done yet");

    public static readonly ChunkResult<ChunkAccess> UnloadedChunk =
        ChunkResult<ChunkAccess>.Error("Unloaded chunk");

    public static readonly Task<ChunkResult<ChunkAccess>> UnloadedChunkFuture =
        Task.FromResult(UnloadedChunk);

    private readonly ChunkPos _pos;
    private ChunkStatus? _highestAllowedStatus;
    private ChunkStatus? _startedWork;
    private readonly TaskCompletionSource<ChunkResult<ChunkAccess>>?[] _futures;
    private ChunkGenerationTask? _task;
    private int _generationRefCount;
    private TaskCompletionSource<bool>? _generationSaveSyncSource;

    protected GenerationChunkHolder(ChunkPos pos)
    {
        if (!pos.IsValid())
        {
            throw new InvalidOperationException(
                $"Trying to create chunk out of reasonable bounds: {pos}");
        }

        _pos = pos;
        _futures = new TaskCompletionSource<ChunkResult<ChunkAccess>>?[ChunkStatuses.Count];
    }

    public ChunkPos GetPos() => _pos;

    public int GetGenerationRefCount() => Volatile.Read(ref _generationRefCount);

    public Task GenerationSaveSyncFuture =>
        Volatile.Read(ref _generationSaveSyncSource)?.Task ?? Task.CompletedTask;

    /// <summary>
    /// Supplies the scheduler-derived status ceiling. The caller owns ticket-level
    /// calculation and must clear disallowed pending futures when lowering this value.
    /// </summary>
    internal void SetHighestAllowedStatus(ChunkStatus? status) =>
        Volatile.Write(ref _highestAllowedStatus, status);

    /// <summary>
    /// Updates an already-calculated status ceiling. Ticket-level calculation remains
    /// the responsibility of the concrete chunk map.
    /// </summary>
    internal void UpdateHighestAllowedStatus(
        ChunkStatus? newStatus,
        GeneratingChunkMap scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        ChunkStatus? oldStatus = Volatile.Read(ref _highestAllowedStatus);
        Volatile.Write(ref _highestAllowedStatus, newStatus);

        bool statusDropped = oldStatus is not null &&
            (newStatus is null || newStatus.IsBefore(oldStatus));

        if (!statusDropped)
            return;

        FailAndClearPendingFuturesBetween(newStatus, oldStatus);

        if (Volatile.Read(ref _task) is not null)
        {
            RescheduleChunkTask(
                scheduler,
                FindHighestStatusWithPendingFuture(newStatus));
        }
    }

    /// <summary>
    /// Returns the request future and starts or extends the scheduled generation task
    /// when the requested status is not already covered by the current task.
    /// </summary>
    internal Task<ChunkResult<ChunkAccess>> ScheduleChunkGenerationTask(
        ChunkStatus status,
        GeneratingChunkMap scheduler)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (IsStatusDisallowed(status))
            return UnloadedChunkFuture;

        Task<ChunkResult<ChunkAccess>> future = GetOrCreateFuture(status);
        if (future.IsCompleted)
            return future;

        ChunkGenerationTask? task = Volatile.Read(ref _task);
        if (task is null || status.IsAfter(task.TargetStatus))
            RescheduleChunkTask(scheduler, status);

        return future;
    }

    /// <summary>
    /// Applies one step only after claiming its exact parent-to-target transition.
    /// A concurrent caller receives the same per-status future instead of executing
    /// the step a second time.
    /// </summary>
    internal Task<ChunkResult<ChunkAccess>> ApplyStep(
        ChunkStep step,
        GeneratingChunkMap chunkMap,
        StaticCache2D<GenerationChunkHolder> cache)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(chunkMap);
        ArgumentNullException.ThrowIfNull(cache);

        if (IsStatusDisallowed(step.TargetStatus))
            return UnloadedChunkFuture;

        return AcquireStatusBump(step.TargetStatus)
            ? ApplyStepCore(step, chunkMap, cache)
            : GetOrCreateFuture(step.TargetStatus);
    }

    internal void RemoveTask(ChunkGenerationTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        Interlocked.CompareExchange(ref _task, null, task);
    }

    private void RescheduleChunkTask(
        GeneratingChunkMap scheduler,
        ChunkStatus? status)
    {
        ChunkGenerationTask? newTask = status is null
            ? null
            : scheduler.ScheduleGenerationTask(status, _pos);

        ChunkGenerationTask? oldTask = Interlocked.Exchange(ref _task, newTask);
        oldTask?.MarkForCancellation();
    }

    private async Task<ChunkResult<ChunkAccess>> ApplyStepCore(
        ChunkStep step,
        GeneratingChunkMap chunkMap,
        StaticCache2D<GenerationChunkHolder> cache)
    {
        try
        {
            ChunkAccess chunk = await chunkMap.ApplyStep(this, step, cache)
                .ConfigureAwait(false);
            CompleteFuture(step.TargetStatus, chunk);
            return ChunkResult<ChunkAccess>.Of(chunk);
        }
        catch (Exception exception)
        {
            Task<ChunkResult<ChunkAccess>> future =
                GetOrCreateFuture(step.TargetStatus);
            if (!ReferenceEquals(future, UnloadedChunkFuture))
            {
                var failure = ChunkResult<ChunkAccess>.Error(
                    () => exception.ToString());
                Volatile.Read(ref _futures[step.TargetStatus.GetIndex()])
                    ?.TrySetResult(failure);
            }

            throw;
        }
    }

    /// <summary>
    /// Gets the future for a status, atomically installing exactly one pending future.
    /// The status ceiling is checked both before and after installation to close the
    /// race with a concurrent ceiling drop.
    /// </summary>
    internal Task<ChunkResult<ChunkAccess>> GetOrCreateFuture(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (IsStatusDisallowed(status))
            return UnloadedChunkFuture;

        int index = status.GetIndex();
        TaskCompletionSource<ChunkResult<ChunkAccess>>? future =
            Volatile.Read(ref _futures[index]);

        while (future is null)
        {
            var newValue = new TaskCompletionSource<ChunkResult<ChunkAccess>>();
            future = Interlocked.CompareExchange(ref _futures[index], newValue, null);

            if (future is null)
            {
                if (IsStatusDisallowed(status))
                {
                    FailAndClearPendingFuture(index, newValue);
                    return UnloadedChunkFuture;
                }

                return newValue.Task;
            }
        }

        return future.Task;
    }

    /// <summary>
    /// Completes the future for a generated status, retrying while a concurrently
    /// failed future is being removed. A second successful completion is an error.
    /// </summary>
    internal void CompleteFuture(ChunkStatus status, ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(chunk);

        var result = ChunkResult<ChunkAccess>.Of(chunk);
        int index = status.GetIndex();

        while (true)
        {
            TaskCompletionSource<ChunkResult<ChunkAccess>>? future =
                Volatile.Read(ref _futures[index]);

            if (future is null)
            {
                var completedFuture = new TaskCompletionSource<ChunkResult<ChunkAccess>>();
                completedFuture.SetResult(result);

                if (Interlocked.CompareExchange(
                        ref _futures[index],
                        completedFuture,
                        null) is null)
                {
                    return;
                }
            }
            else
            {
                if (future.TrySetResult(result))
                    return;

                if (GetNow(future.Task).IsSuccess())
                {
                    throw new InvalidOperationException(
                        "Trying to complete a future but found it to be completed successfully already");
                }

                Thread.Yield();
            }
        }
    }

    /// <summary>
    /// Fails and removes pending futures for statuses in (fromExclusive, toInclusive].
    /// Completed futures are left untouched, matching the holder's unload transition.
    /// </summary>
    internal void FailAndClearPendingFuturesBetween(
        ChunkStatus? fromExclusive,
        ChunkStatus toInclusive)
    {
        ArgumentNullException.ThrowIfNull(toInclusive);

        int start = fromExclusive is null ? 0 : fromExclusive.GetIndex() + 1;
        int end = toInclusive.GetIndex();

        for (int index = start; index <= end; index++)
        {
            TaskCompletionSource<ChunkResult<ChunkAccess>>? previous =
                Volatile.Read(ref _futures[index]);

            if (previous is not null)
                FailAndClearPendingFuture(index, previous);
        }
    }

    private void FailAndClearPendingFuture(
        int index,
        TaskCompletionSource<ChunkResult<ChunkAccess>> previous)
    {
        if (previous.TrySetResult(UnloadedChunk) &&
            !ReferenceEquals(
                Interlocked.CompareExchange(ref _futures[index], null, previous),
                previous))
        {
            throw new InvalidOperationException(
                "Nothing else should replace the future here");
        }
    }

    internal ChunkStatus? FindHighestStatusWithPendingFuture(ChunkStatus? newStatus)
    {
        if (newStatus is null)
            return null;

        ChunkStatus? alreadyStarted = Volatile.Read(ref _startedWork);
        ChunkStatus highestStatus = newStatus;

        while (alreadyStarted is null || highestStatus.IsAfter(alreadyStarted))
        {
            if (Volatile.Read(ref _futures[highestStatus.GetIndex()]) is not null)
                return highestStatus;

            if (ReferenceEquals(highestStatus, ChunkStatus.EMPTY))
                break;

            highestStatus = highestStatus.GetParent();
        }

        return null;
    }

    /// <summary>
    /// Atomically claims the next status in the chain. Returns false when this status
    /// or a later one has already been claimed; an out-of-order transition throws.
    /// </summary>
    internal bool AcquireStatusBump(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        ChunkStatus? parent = ReferenceEquals(status, ChunkStatus.EMPTY)
            ? null
            : status.GetParent();

        ChunkStatus? previousStarted =
            Interlocked.CompareExchange(ref _startedWork, status, parent);

        if (ReferenceEquals(previousStarted, parent))
            return true;

        if (previousStarted is not null && !status.IsAfter(previousStarted))
            return false;

        throw new InvalidOperationException(
            $"Unexpected last startedWork status: {previousStarted} while trying to start: {status}");
    }

    private bool IsStatusDisallowed(ChunkStatus status)
    {
        ChunkStatus? highestAllowedStatus = Volatile.Read(ref _highestAllowedStatus);
        return highestAllowedStatus is null || status.IsAfter(highestAllowedStatus);
    }

    private static ChunkResult<ChunkAccess> GetNow(
        Task<ChunkResult<ChunkAccess>> future) =>
        future.IsCompleted
            ? future.GetAwaiter().GetResult()
            : NotDoneYet;

    public ChunkAccess? GetChunkIfPresentUnchecked(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        TaskCompletionSource<ChunkResult<ChunkAccess>>? future =
            Volatile.Read(ref _futures[status.GetIndex()]);

        return future is null
            ? null
            : GetNow(future.Task).OrElse(null);
    }

    public ChunkAccess? GetChunkIfPresent(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return IsStatusDisallowed(status)
            ? null
            : GetChunkIfPresentUnchecked(status);
    }

    public ChunkAccess? GetLatestChunk()
    {
        ChunkStatus? status = Volatile.Read(ref _startedWork);
        if (status is null)
            return null;

        ChunkAccess? chunk = GetChunkIfPresentUnchecked(status);
        return chunk ?? GetChunkIfPresentUnchecked(status.GetParent());
    }

    public ChunkStatus? GetPersistedStatus()
    {
        TaskCompletionSource<ChunkResult<ChunkAccess>>? future =
            Volatile.Read(ref _futures[ChunkStatus.EMPTY.GetIndex()]);

        ChunkAccess? chunk = future is null
            ? null
            : GetNow(future.Task).OrElse(null);

        return chunk?.GetPersistedStatus();
    }

    public ChunkStatus? GetLatestStatus()
    {
        ChunkStatus? status = Volatile.Read(ref _startedWork);
        if (status is null)
            return null;

        return GetChunkIfPresentUnchecked(status) is not null
            ? status
            : status.GetParent();
    }

    public IReadOnlyList<KeyValuePair<ChunkStatus, Task<ChunkResult<ChunkAccess>>?>>
        GetAllFutures()
    {
        var result = new List<KeyValuePair<ChunkStatus, Task<ChunkResult<ChunkAccess>>?>>(
            ChunkStatuses.Count);

        for (int index = 0; index < ChunkStatuses.Count; index++)
        {
            TaskCompletionSource<ChunkResult<ChunkAccess>>? future =
                Volatile.Read(ref _futures[index]);

            result.Add(new KeyValuePair<ChunkStatus, Task<ChunkResult<ChunkAccess>>?>(
                ChunkStatuses[index],
                future?.Task));
        }

        return result;
    }

    public void IncreaseGenerationRefCount()
    {
        if (Interlocked.Increment(ref _generationRefCount) == 1)
        {
            var source = new TaskCompletionSource<bool>();
            Volatile.Write(ref _generationSaveSyncSource, source);
            AddSaveDependency(source.Task);
        }
    }

    public void DecreaseGenerationRefCount()
    {
        TaskCompletionSource<bool>? source =
            Volatile.Read(ref _generationSaveSyncSource);
        int newValue = Interlocked.Decrement(ref _generationRefCount);

        if (newValue == 0)
            source?.TrySetResult(true);

        if (newValue < 0)
        {
            throw new InvalidOperationException(
                $"More releases than claims. Count: {newValue}");
        }
    }

    protected abstract void AddSaveDependency(Task sync);
}
