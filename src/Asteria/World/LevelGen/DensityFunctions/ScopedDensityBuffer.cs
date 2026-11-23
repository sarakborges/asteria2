namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// A density buffer with an arena-owned lease and the Minecraft 26.3 restore/close lifecycle.
/// </summary>
public sealed class ScopedDensityBuffer : DensityBuffer, IDisposable
{
    private readonly DensityBufferArena _arena;
    private int _age;
    private bool _closed;

    internal ScopedDensityBuffer(DensityBufferArena arena, int capacity, int size)
        : base(capacity)
    {
        _arena = arena ?? throw new ArgumentNullException(nameof(arena));
        CurrentSize = size;
    }

    public int Age => _age;

    public bool IsClosed => _closed;

    internal void Restore(int size)
    {
        if (size > Capacity())
            throw new ArgumentException("Cannot set size larger than buffer capacity");

        if (!_closed)
            throw new ArgumentException("Buffer is already in use");

        _age = 0;
        _closed = false;
        CurrentSize = size;
    }

    internal int IncrementAge() => ++_age;

    public void Close()
    {
        if (!_closed)
        {
            _closed = true;
            _arena.Release(this);
        }
    }

    public void Dispose() => Close();
}
