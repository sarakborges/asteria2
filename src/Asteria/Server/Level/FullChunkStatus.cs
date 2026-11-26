namespace Asteria.Server.Level;

/// <summary>
/// Describes how far a chunk has progressed through the server-side full-chunk lifecycle.
/// Mirrors Minecraft Java 26.3 FullChunkStatus.
/// </summary>
public enum FullChunkStatus
{
    INACCESSIBLE,
    FULL,
    BLOCK_TICKING,
    ENTITY_TICKING
}

/// <summary>
/// Operations corresponding to methods declared on the Java enum.
/// </summary>
public static class FullChunkStatusExtensions
{
    /// <summary>
    /// Returns whether this status is at or beyond the supplied lifecycle status.
    /// The enum values retain the declaration order used by Java ordinal comparison.
    /// </summary>
    public static bool IsOrAfter(this FullChunkStatus status, FullChunkStatus step) =>
        (int)status >= (int)step;
}
