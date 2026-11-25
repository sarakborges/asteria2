using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.Level;
using Asteria.Util;

internal static class ChunkGenerationTaskChecks
{
    public static async Task Run()
    {
        await CheckImmediateCenterCompletion();
        await CheckWaitAndResume();
        await CheckFailureCancelsAndReleasesClaims();
        CheckMaximumGenerationWindowAndCancellation();
        CheckTaskExtensionAndStatusCeilingDrop();
    }

    private static Task CheckImmediateCenterCompletion()
    {
        ChunkPos position = ChunkPos.Zero;
        var chunk = CreateChunk(position);
        var map = new TestMap((_, step) =>
        {
            Require(
                ReferenceEquals(step.TargetStatus, ChunkStatus.EMPTY),
                "An EMPTY target must begin by scheduling the EMPTY step.");
            return Task.FromResult<ChunkAccess>(chunk);
        });

        ChunkGenerationTask task = ChunkGenerationTask.Create(
            map,
            ChunkStatus.EMPTY,
            position);

        Require(
            map.AcquisitionOrder.SequenceEqual(new[] { position }) &&
            task.GetCenter().GetPos() == position,
            "An EMPTY request must acquire only its center holder.");

        Require(
            task.RunUntilWait() is null,
            "A synchronously completed EMPTY step must finish without a wait point.");

        GenerationChunkHolder center = map.GetHolder(position);
        Require(
            center.GetPersistedStatus() is not null &&
            ReferenceEquals(center.GetChunkIfPresentUnchecked(ChunkStatus.EMPTY), chunk),
            "The completed center step must publish its chunk in the EMPTY future.");
        Require(
            center.GetGenerationRefCount() == 0 &&
            center.GenerationSaveSyncFuture.IsCompleted &&
            map.ReleaseCount == 1,
            "Completing the target must release the center holder's generation claim.");
        return Task.CompletedTask;
    }

    private static async Task CheckWaitAndResume()
    {
        ChunkPos position = new(4, -3);
        var chunk = CreateChunk(position);
        var pending = new TaskCompletionSource<ChunkAccess>();
        var map = new TestMap((_, _) => pending.Task);
        ChunkGenerationTask task = ChunkGenerationTask.Create(
            map,
            ChunkStatus.EMPTY,
            position);

        Task? waitingFor = task.RunUntilWait();
        Require(
            waitingFor is not null && !waitingFor.IsCompleted,
            "An incomplete layer future must be returned as the task's wait point.");

        pending.SetResult(chunk);
        await waitingFor!.ConfigureAwait(false);

        Require(
            task.RunUntilWait() is null,
            "Resuming after the layer future completes must finish the target layer.");
        Require(
            map.ReleaseCount == 1 &&
            map.GetHolder(position).GetGenerationRefCount() == 0,
            "Resuming a completed task must release the acquired generation claims exactly once.");
    }

    private static async Task CheckFailureCancelsAndReleasesClaims()
    {
        ChunkPos position = new(-7, 12);
        var pending = new TaskCompletionSource<ChunkAccess>();
        var map = new TestMap((_, _) => pending.Task);
        ChunkGenerationTask task = ChunkGenerationTask.Create(
            map,
            ChunkStatus.EMPTY,
            position);

        Task? waitingFor = task.RunUntilWait();
        Require(waitingFor is not null, "The failing stage must initially wait on its incomplete future.");

        pending.SetException(new InvalidOperationException("test generation failure"));
        await waitingFor!.ConfigureAwait(false);

        Require(
            task.RunUntilWait() is null,
            "A failed layer result must cancel the generation task and finish its release path.");

        ChunkResult<ChunkAccess> result = map.GetHolder(position)
            .GetOrCreateFutureForTest(ChunkStatus.EMPTY);
        Require(
            !result.IsSuccess() &&
            result.GetError() is string error &&
            error.Contains("test generation failure", StringComparison.Ordinal),
            "The status future must retain a typed failure containing the original generation exception.");
        Require(
            map.ReleaseCount == 1 &&
            map.GetHolder(position).GetGenerationRefCount() == 0,
            "A failed stage must release every acquired generation claim.");
    }

    private static void CheckMaximumGenerationWindowAndCancellation()
    {
        ChunkPos position = new(17, -23);
        var map = new TestMap(static (_, _) =>
            throw new InvalidOperationException("A cancelled task must not apply a step."));

        ChunkGenerationTask task = ChunkGenerationTask.Create(
            map,
            ChunkStatus.FULL,
            position);

        Require(
            map.AcquisitionOrder.Count == 23 * 23,
            "FULL generation must acquire the complete 23-by-23 holder window (529 chunks).");
        Require(
            map.AcquisitionOrder[0] == new ChunkPos(position.X - 11, position.Z - 11) &&
            map.AcquisitionOrder[1] == new ChunkPos(position.X - 11, position.Z - 10) &&
            map.AcquisitionOrder[^1] == new ChunkPos(position.X + 11, position.Z + 11),
            "Generation claims must follow the reference X-outer, Z-inner acquisition order.");

        task.MarkForCancellation();
        Require(
            task.RunUntilWait() is null,
            "A cancelled task with no pending layer must complete its release path immediately.");
        Require(
            map.ReleaseCount == 529 &&
            map.Holders.Values.All(holder =>
                holder.GetGenerationRefCount() == 0 &&
                holder.GenerationSaveSyncFuture.IsCompleted),
            "Cancellation must release every acquired generation claim and complete each save dependency.");
    }


    private static void CheckTaskExtensionAndStatusCeilingDrop()
    {
        ChunkPos position = new(29, -41);
        var map = new TestMap(static (_, _) =>
            throw new InvalidOperationException("Cancelled tasks must not begin another layer."));
        TestHolder holder = map.CreateHolder(position);

        Task<ChunkResult<ChunkAccess>> structureStarts =
            holder.Request(ChunkStatus.STRUCTURE_STARTS, map);
        ChunkGenerationTask firstTask = map.ScheduledTasks.Single();
        Require(
            ReferenceEquals(firstTask.TargetStatus, ChunkStatus.STRUCTURE_STARTS),
            "The first requested status must be the active task target.");

        Task<ChunkResult<ChunkAccess>> biomes =
            holder.Request(ChunkStatus.BIOMES, map);
        ChunkGenerationTask extendedTask = map.ScheduledTasks[1];
        Require(
            ReferenceEquals(extendedTask.TargetStatus, ChunkStatus.BIOMES),
            "A request beyond the active target must replace it with a task for the higher status.");
        Require(
            firstTask.RunUntilWait() is null && map.ApplyStepCount == 0,
            "Extending the task must cancel the previous task before it schedules another layer.");

        holder.UpdateAllowedStatus(ChunkStatus.STRUCTURE_STARTS, map);

        Require(
            biomes.IsCompleted &&
            !biomes.GetAwaiter().GetResult().IsSuccess() &&
            biomes.GetAwaiter().GetResult().GetError() == "Unloaded chunk",
            "Lowering the allowed status must fail and clear futures above the new ceiling.");
        Require(
            !structureStarts.IsCompleted,
            "Lowering the ceiling must retain a pending future at the newly allowed status.");

        ChunkGenerationTask replacementTask = map.ScheduledTasks[2];
        Require(
            ReferenceEquals(replacementTask.TargetStatus, ChunkStatus.STRUCTURE_STARTS),
            "The holder must reschedule its highest remaining pending status after a ceiling drop.");
        Require(
            extendedTask.RunUntilWait() is null,
            "The task superseded by the ceiling drop must be cancelled and release its claims.");

        replacementTask.MarkForCancellation();
        Require(
            replacementTask.RunUntilWait() is null,
            "The replacement task must release its claims when explicitly cancelled.");
        Require(
            map.ReleaseCount == map.AcquisitionOrder.Count &&
            map.Holders.Values.All(value => value.GetGenerationRefCount() == 0),
            "All claims from the superseded, extended and replacement tasks must be released exactly once.");
    }

    private static ProtoChunk CreateChunk(ChunkPos position) =>
        new(
            position,
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

    private sealed class TestMap : GeneratingChunkMap
    {
        private readonly Func<GenerationChunkHolder, ChunkStep, Task<ChunkAccess>> _applyStep;

        public Dictionary<long, TestHolder> Holders { get; } = new();
        public List<ChunkPos> AcquisitionOrder { get; } = new();
        public List<ChunkGenerationTask> ScheduledTasks { get; } = new();
        public int ReleaseCount { get; private set; }
        public int ApplyStepCount { get; private set; }

        public TestMap(Func<GenerationChunkHolder, ChunkStep, Task<ChunkAccess>> applyStep)
        {
            _applyStep = applyStep;
        }

        public GenerationChunkHolder AcquireGeneration(long chunkNode)
        {
            ChunkPos position = ChunkPos.Unpack(chunkNode);
            TestHolder holder = CreateHolder(position);
            holder.IncreaseGenerationRefCount();

            AcquisitionOrder.Add(position);
            return holder;
        }

        public TestHolder CreateHolder(ChunkPos position)
        {
            long key = ChunkPos.Pack(position.X, position.Z);
            if (Holders.TryGetValue(key, out TestHolder? existing))
                return existing;

            var holder = new TestHolder(position);
            holder.AllowThrough(ChunkStatus.FULL);
            Holders.Add(key, holder);
            return holder;
        }

        public Task<ChunkAccess> ApplyStep(
            GenerationChunkHolder chunkHolder,
            ChunkStep step,
            StaticCache2D<GenerationChunkHolder> cache)
        {
            ApplyStepCount++;
            return _applyStep(chunkHolder, step);
        }

        public void ReleaseGeneration(GenerationChunkHolder chunkHolder)
        {
            chunkHolder.DecreaseGenerationRefCount();
            ReleaseCount++;
        }

        public ChunkGenerationTask ScheduleGenerationTask(
            ChunkStatus targetStatus,
            ChunkPos position)
        {
            ChunkGenerationTask task = ChunkGenerationTask.Create(
                this,
                targetStatus,
                position);
            ScheduledTasks.Add(task);
            return task;
        }

        public void RunGenerationTasks()
        {
        }

        public TestHolder GetHolder(ChunkPos position) =>
            Holders[ChunkPos.Pack(position.X, position.Z)];
    }

    private sealed class TestHolder : GenerationChunkHolder
    {
        public TestHolder(ChunkPos position) : base(position)
        {
        }

        public void AllowThrough(ChunkStatus status) =>
            SetHighestAllowedStatus(status);

        public Task<ChunkResult<ChunkAccess>> Request(
            ChunkStatus status,
            GeneratingChunkMap scheduler) =>
            ScheduleChunkGenerationTask(status, scheduler);

        public void UpdateAllowedStatus(
            ChunkStatus? status,
            GeneratingChunkMap scheduler) =>
            UpdateHighestAllowedStatus(status, scheduler);

        public ChunkResult<ChunkAccess> GetOrCreateFutureForTest(ChunkStatus status) =>
            GetOrCreateFuture(status).GetAwaiter().GetResult();

        protected override void AddSaveDependency(Task sync)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
