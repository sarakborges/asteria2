using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.Level;

internal static class ChunkGenerationTaskChecks
{
    public static async Task Run()
    {
        await CheckImmediateCenterCompletion();
        await CheckWaitAndResume();
        await CheckFailureCancelsAndReleasesClaims();
        CheckMaximumGenerationWindowAndCancellation();
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
        public int ReleaseCount { get; private set; }

        public TestMap(Func<GenerationChunkHolder, ChunkStep, Task<ChunkAccess>> applyStep)
        {
            _applyStep = applyStep;
        }

        public GenerationChunkHolder AcquireGeneration(long chunkNode)
        {
            ChunkPos position = ChunkPos.Unpack(chunkNode);
            var holder = new TestHolder(position);
            holder.AllowThrough(ChunkStatus.FULL);
            holder.IncreaseGenerationRefCount();

            Holders.Add(chunkNode, holder);
            AcquisitionOrder.Add(position);
            return holder;
        }

        public Task<ChunkAccess> ApplyStep(
            GenerationChunkHolder chunkHolder,
            ChunkStep step,
            StaticCache2D<GenerationChunkHolder> cache) =>
            _applyStep(chunkHolder, step);

        public void ReleaseGeneration(GenerationChunkHolder chunkHolder)
        {
            chunkHolder.DecreaseGenerationRefCount();
            ReleaseCount++;
        }

        public ChunkGenerationTask ScheduleGenerationTask(
            ChunkStatus targetStatus,
            ChunkPos position) =>
            throw new NotSupportedException(
                "The test map does not implement task dispatch.");

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
