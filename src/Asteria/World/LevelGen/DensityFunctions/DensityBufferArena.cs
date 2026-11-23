namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Owns scoped density buffers. The vanilla GLOBAL arena allocates per request and releases no-op.
/// </summary>
public interface DensityBufferArena
{
    static DensityBufferArena Global { get; } = new GlobalDensityBufferArena();

    ScopedDensityBuffer Acquire(int size);

    void Release(ScopedDensityBuffer buffer);

    private sealed class GlobalDensityBufferArena : DensityBufferArena
    {
        public ScopedDensityBuffer Acquire(int size) =>
            new(this, size, size);

        public void Release(ScopedDensityBuffer buffer)
        {
        }
    }
}
