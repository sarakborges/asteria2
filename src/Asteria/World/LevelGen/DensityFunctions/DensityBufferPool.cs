using Asteria.Core;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Reusable scoped density-buffer pool matching Minecraft Java 26.3 DensityBufferPool.
/// </summary>
public sealed class DensityBufferPool : DensityBufferArena
{
    private const int BufferSizeIncrement = 16;
    private const int MaxReuseSizeFactor = 2;

    private readonly int _maxAge;
    private readonly List<ScopedDensityBuffer> _buffers = new();

    public DensityBufferPool(int maxAge) => _maxAge = maxAge;

    public ScopedDensityBuffer Acquire(int size)
    {
        int roundedMinSize = Mth.RoundToward(size, BufferSizeIncrement);
        ScopedDensityBuffer? buffer = TryTakeBest(
            roundedMinSize,
            unchecked(roundedMinSize * MaxReuseSizeFactor));

        if (buffer is null)
            return new ScopedDensityBuffer(this, roundedMinSize, size);

        buffer.Restore(size);
        return buffer;
    }

    private ScopedDensityBuffer? TryTakeBest(int minCapacity, int maxCapacity)
    {
        int bestIndex = -1;
        int bestCapacity = unchecked(maxCapacity + 1);

        for (int index = _buffers.Count - 1; index >= 0; index--)
        {
            int capacity = _buffers[index].Capacity();
            if (capacity == minCapacity)
            {
                ScopedDensityBuffer exact = _buffers[index];
                _buffers.RemoveAt(index);
                return exact;
            }

            if (capacity > minCapacity && capacity < bestCapacity)
            {
                bestIndex = index;
                bestCapacity = capacity;
            }
        }

        if (bestIndex == -1)
            return null;

        ScopedDensityBuffer best = _buffers[bestIndex];
        _buffers.RemoveAt(bestIndex);
        return best;
    }

    public void Release(ScopedDensityBuffer buffer) =>
        _buffers.Add(buffer);

    public void GarbageCollect() =>
        _buffers.RemoveAll(buffer => buffer.IncrementAge() > _maxAge);

    public void Clear() => _buffers.Clear();

    public bool IsEmpty() => _buffers.Count == 0;

    internal int Count => _buffers.Count;
}
