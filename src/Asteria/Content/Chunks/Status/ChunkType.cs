namespace Asteria.Content.Chunks.Status;

/// <summary>
/// Distinguishes a chunk under generation from a chunk attached to a level.
/// Mirrors the two values of Minecraft Java 26.3 ChunkType.
/// </summary>
public enum ChunkType
{
    PROTOCHUNK,
    LEVELCHUNK
}
