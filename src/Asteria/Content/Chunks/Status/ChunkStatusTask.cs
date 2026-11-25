using Asteria.Content.Chunks;
using Asteria.Util;
using Asteria.World.LevelGen;

namespace Asteria.Content.Chunks.Status;

/// <summary>
/// Work performed when a chunk advances to a generation status.
/// The local offline runtime resolves the dependency window before invoking the task;
/// unlike the server reference, this delegate receives chunk snapshots rather than
/// GenerationChunkHolder instances because Asteria has no concurrent holder scheduler yet.
/// </summary>
public delegate ValueTask<ChunkAccess> ChunkStatusTask(
    WorldGenContext context,
    ChunkStep step,
    StaticCache2D<ChunkAccess?> chunks,
    ChunkAccess chunk);
