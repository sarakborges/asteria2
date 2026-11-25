using Asteria.Content.Chunks;
using Asteria.Content.Chunks.Status;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.Level;

internal static class GenerationChunkHolderChecks
{
    public static void Run()
    {
        CheckPositionAndInitialStatusCeiling();
        CheckPerStatusFutureCreation();
        CheckFutureCompletionAndChunkLookups();
        CheckPendingFutureFailureAndRemoval();
        CheckAtomicStatusClaims();
        CheckGenerationReferenceCounting();
    }

    private static void CheckPositionAndInitialStatusCeiling()
    {
        ChunkPos invalidPosition = new(ChunkPyramid.MaxChunkCoordinateValue + 1, 0);
        RequireThrows<InvalidOperationException>(
            () => _ = new TestHolder(invalidPosition),
            "A generation holder must reject a chunk position outside the supported world border.");

        var holder = new TestHolder(ChunkPos.Zero);
        Require(
            ReferenceEquals(
                holder.GetOrCreateFuture(ChunkStatus.EMPTY),
                GenerationChunkHolder.UnloadedChunkFuture),
            "A holder without an allowed status must return the shared unloaded-chunk future.");
    }

    private static void CheckPerStatusFutureCreation()
    {
        var holder = new TestHolder(ChunkPos.Zero);
        holder.AllowThrough(ChunkStatus.FULL);

        Task<ChunkResult<ChunkAccess>> first =
            holder.GetOrCreateFuture(ChunkStatus.STRUCTURE_STARTS);
        Task<ChunkResult<ChunkAccess>> repeated =
            holder.GetOrCreateFuture(ChunkStatus.STRUCTURE_STARTS);
        Task<ChunkResult<ChunkAccess>> anotherStatus =
            holder.GetOrCreateFuture(ChunkStatus.STRUCTURE_REFERENCES);

        Require(
            ReferenceEquals(first, repeated),
            "Concurrent requests for one status must observe the same stored future.");
        Require(
            !ReferenceEquals(first, anotherStatus),
            "Each chunk status must own a separate future.");

        var concurrentResults = new Task<ChunkResult<ChunkAccess>>[64];
        Parallel.For(
            0,
            concurrentResults.Length,
            index => concurrentResults[index] =
                holder.GetOrCreateFuture(ChunkStatus.BIOMES));

        Require(
            concurrentResults.All(future => ReferenceEquals(future, concurrentResults[0])),
            "The compare-exchange installation must publish exactly one future under concurrent requests.");

        IReadOnlyList<KeyValuePair<ChunkStatus, Task<ChunkResult<ChunkAccess>>?>> allFutures =
            holder.GetAllFutures();
        Require(
            allFutures.Count == ChunkStatus.GetStatusList().Count &&
            ReferenceEquals(
                allFutures[ChunkStatus.STRUCTURE_STARTS.GetIndex()].Value,
                first),
            "The debug future view must preserve status order and expose the stored future.");
    }

    private static void CheckFutureCompletionAndChunkLookups()
    {
        var holder = new TestHolder(ChunkPos.Zero);
        holder.AllowThrough(ChunkStatus.FULL);

        var chunk = new ProtoChunk(
            ChunkPos.Zero,
            LevelHeightAccessor.Create(-16, 48),
            PalettedContainerFactory.CreateBuiltIn());

        Task<ChunkResult<ChunkAccess>> emptyFuture =
            holder.GetOrCreateFuture(ChunkStatus.EMPTY);
        holder.CompleteFuture(ChunkStatus.EMPTY, chunk);

        ChunkResult<ChunkAccess> emptyResult = emptyFuture.GetAwaiter().GetResult();
        Require(
            emptyResult.IsSuccess() &&
            ReferenceEquals(emptyResult.OrElse(null), chunk),
            "Completing a status must publish its chunk through the existing future.");
        Require(
            ReferenceEquals(holder.GetChunkIfPresent(ChunkStatus.EMPTY), chunk) &&
            ReferenceEquals(holder.GetChunkIfPresentUnchecked(ChunkStatus.EMPTY), chunk),
            "Completed successful futures must resolve through the checked and unchecked lookups.");
        Require(
            ReferenceEquals(holder.GetPersistedStatus(), ChunkStatus.EMPTY),
            "The persisted status must come from the completed EMPTY future's chunk.");

        Require(holder.Acquire(ChunkStatus.EMPTY), "The EMPTY stage must be claimable from the initial state.");
        Require(holder.Acquire(ChunkStatus.STRUCTURE_STARTS), "The next stage must be claimable from its exact parent.");

        Require(
            ReferenceEquals(holder.GetLatestChunk(), chunk) &&
            ReferenceEquals(holder.GetLatestStatus(), ChunkStatus.EMPTY),
            "An uncompleted started stage must fall back to its parent chunk and status.");

        holder.CompleteFuture(ChunkStatus.STRUCTURE_STARTS, chunk);
        Require(
            ReferenceEquals(holder.GetLatestChunk(), chunk) &&
            ReferenceEquals(holder.GetLatestStatus(), ChunkStatus.STRUCTURE_STARTS),
            "A completed started stage must become the latest chunk and status.");

        RequireThrows<InvalidOperationException>(
            () => holder.CompleteFuture(ChunkStatus.STRUCTURE_STARTS, chunk),
            "Completing a status future successfully twice must fail.");

        holder.AllowThrough(ChunkStatus.EMPTY);
        Require(
            holder.GetChunkIfPresent(ChunkStatus.STRUCTURE_STARTS) is null &&
            ReferenceEquals(holder.GetChunkIfPresentUnchecked(ChunkStatus.STRUCTURE_STARTS), chunk),
            "The checked lookup must respect the status ceiling while the unchecked lookup must not.");
    }

    private static void CheckPendingFutureFailureAndRemoval()
    {
        var holder = new TestHolder(ChunkPos.Zero);
        holder.AllowThrough(ChunkStatus.FULL);

        Task<ChunkResult<ChunkAccess>> biomes =
            holder.GetOrCreateFuture(ChunkStatus.BIOMES);
        Task<ChunkResult<ChunkAccess>> terrain =
            holder.GetOrCreateFuture(ChunkStatus.TERRAIN);
        Task<ChunkResult<ChunkAccess>> light =
            holder.GetOrCreateFuture(ChunkStatus.LIGHT);

        holder.FailAndClearPendingFuturesBetween(
            ChunkStatus.BIOMES,
            ChunkStatus.LIGHT);

        ChunkResult<ChunkAccess> terrainResult = terrain.GetAwaiter().GetResult();
        ChunkResult<ChunkAccess> lightResult = light.GetAwaiter().GetResult();

        Require(
            !terrainResult.IsSuccess() &&
            terrainResult.GetError() == "Unloaded chunk" &&
            !lightResult.IsSuccess() &&
            lightResult.GetError() == "Unloaded chunk",
            "Pending futures in the exclusive/inclusive status interval must complete with the unloaded result.");
        Require(
            ReferenceEquals(holder.GetOrCreateFuture(ChunkStatus.TERRAIN), terrain) == false,
            "A failed pending future must be removed so a later request can create a new future.");
        Require(
            ReferenceEquals(holder.GetOrCreateFuture(ChunkStatus.BIOMES), biomes),
            "Statuses before the cleared interval must retain their existing future.");
        Task<ChunkResult<ChunkAccess>> replacementTerrain =
            holder.GetOrCreateFuture(ChunkStatus.TERRAIN);
        Require(
            !ReferenceEquals(replacementTerrain, terrain),
            "A failed pending future must be replaced by a new future for the same status.");

        Require(
            ReferenceEquals(
                holder.FindHighestStatusWithPendingFuture(ChunkStatus.LIGHT),
                ChunkStatus.TERRAIN),
            "The highest pending future must be the replacement TERRAIN future above the retained BIOMES future.");

        var pendingHolder = new TestHolder(ChunkPos.Zero);
        pendingHolder.AllowThrough(ChunkStatus.FULL);
        _ = pendingHolder.GetOrCreateFuture(ChunkStatus.TERRAIN);
        Require(pendingHolder.Acquire(ChunkStatus.EMPTY), "The initial status must be claimable.");
        Require(
            ReferenceEquals(
                pendingHolder.FindHighestStatusWithPendingFuture(ChunkStatus.LIGHT),
                ChunkStatus.TERRAIN),
            "The highest pending future must be selected without crossing the started-work boundary.");
        Require(
            pendingHolder.FindHighestStatusWithPendingFuture(null) is null,
            "A null requested status must not select a pending generation stage.");
    }

    private static void CheckAtomicStatusClaims()
    {
        var holder = new TestHolder(ChunkPos.Zero);

        Require(holder.Acquire(ChunkStatus.EMPTY), "The initial EMPTY status must be acquired once.");
        Require(!holder.Acquire(ChunkStatus.EMPTY), "A repeated claim for the same status must not start the stage twice.");
        Require(
            holder.Acquire(ChunkStatus.STRUCTURE_STARTS),
            "The immediately following stage must be acquired from its parent.");
        Require(
            !holder.Acquire(ChunkStatus.EMPTY),
            "A request for an earlier stage must not move started work backwards.");

        RequireThrows<InvalidOperationException>(
            () => holder.Acquire(ChunkStatus.BIOMES),
            "A stage that skips its immediate parent must be rejected as an unexpected transition.");
    }

    private static void CheckGenerationReferenceCounting()
    {
        var holder = new TestHolder(ChunkPos.Zero);
        Task initialSync = holder.GenerationSaveSyncFuture;
        Require(
            initialSync.IsCompleted && holder.SaveDependencies.Count == 0,
            "The generation save dependency must start in the completed state.");

        holder.IncreaseGenerationRefCount();
        Task activeSync = holder.GenerationSaveSyncFuture;
        Require(
            holder.GetGenerationRefCount() == 1 &&
            !activeSync.IsCompleted &&
            holder.SaveDependencies.Count == 1 &&
            ReferenceEquals(holder.SaveDependencies[0], activeSync),
            "The first generation claim must create and register one pending save dependency.");

        holder.IncreaseGenerationRefCount();
        Require(
            holder.GetGenerationRefCount() == 2 &&
            ReferenceEquals(holder.GenerationSaveSyncFuture, activeSync) &&
            holder.SaveDependencies.Count == 1,
            "Additional claims must share the existing save dependency.");

        holder.DecreaseGenerationRefCount();
        Require(
            holder.GetGenerationRefCount() == 1 && !activeSync.IsCompleted,
            "The save dependency must remain pending while one generation claim remains.");

        holder.DecreaseGenerationRefCount();
        Require(
            holder.GetGenerationRefCount() == 0 && activeSync.IsCompletedSuccessfully,
            "The final release must complete the save dependency.");

        InvalidOperationException underflow = RequireThrows<InvalidOperationException>(
            holder.DecreaseGenerationRefCount,
            "Releasing more generation claims than acquired must fail.");
        Require(
            underflow.Message == "More releases than claims. Count: -1",
            "Generation claim underflow must preserve the reference error message.");
    }

    private sealed class TestHolder : GenerationChunkHolder
    {
        public List<Task> SaveDependencies { get; } = new();

        public TestHolder(ChunkPos position) : base(position)
        {
        }

        public void AllowThrough(ChunkStatus? status) =>
            SetHighestAllowedStatus(status);

        public bool Acquire(ChunkStatus status) =>
            AcquireStatusBump(status);

        protected override void AddSaveDependency(Task sync) =>
            SaveDependencies.Add(sync);
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
