using System.Collections;

namespace Asteria.Content.Chunks;

/// <summary>
/// Per-terrain-task bit mask of positions carved in one target chunk.
/// Carvers mark geometry here; the terrain generator later asks the aquifer
/// which block state belongs at each marked position.
/// Port of Minecraft Java 26.3 CarvingMask.
/// </summary>
public sealed class CarvingMask : CarverOutput
{
    private readonly int _minY;
    private readonly int _maxY;
    private readonly int _height;
    private BitArray _mask;

    public delegate bool Filter(int x, int y, int z);

    public delegate void Visitor(int x, int z, int bottomY, int topY);

    public CarvingMask(int minY, int maxY)
    {
        _minY = minY;
        _maxY = maxY;
        _height = unchecked(maxY - minY + 1);

        int initialCapacity = unchecked(256 * _height);
        if (initialCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(maxY));

        _mask = new BitArray(initialCapacity);
    }

    public int MinY() => _minY;

    public int MaxY() => _maxY;

    public void Carve(int x, int y, int z)
    {
        int index = GetIndex(x, y, z);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(x));

        EnsureCapacity(index);
        _mask[index] = true;
    }

    /// <summary>
    /// Visits contiguous runs of carved bits as vertical column segments.
    /// The bit order is Y, then Z, then X, matching the reference packed index.
    /// </summary>
    public void Visit(Visitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        int startIndex = NextSetBit(0);
        while (startIndex != -1)
        {
            int endIndex = NextClearBit(startIndex) - 1;
            VisitSegment(visitor, startIndex, endIndex);
            startIndex = NextSetBit(unchecked(endIndex + 1));
        }
    }

    public bool IsEmpty()
    {
        for (int index = 0; index < _mask.Length; index++)
        {
            if (_mask[index])
                return false;
        }

        return true;
    }

    private int GetIndex(int x, int y, int z) =>
        unchecked(y - _minY + (z + (x << 4)) * _height);

    private void EnsureCapacity(int index)
    {
        if (index < _mask.Length)
            return;

        int requiredLength = checked(index + 1);
        int doubledLength = _mask.Length == 0
            ? 1
            : _mask.Length <= int.MaxValue / 2
                ? _mask.Length * 2
                : int.MaxValue;
        _mask.Length = Math.Max(requiredLength, doubledLength);
    }

    private int NextSetBit(int fromIndex)
    {
        if (fromIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));

        for (int index = fromIndex; index < _mask.Length; index++)
        {
            if (_mask[index])
                return index;
        }

        return -1;
    }

    private int NextClearBit(int fromIndex)
    {
        if (fromIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));

        for (int index = fromIndex; ; index++)
        {
            if (index >= _mask.Length || !_mask[index])
                return index;
        }
    }

    private void VisitSegment(Visitor visitor, int startIndex, int endIndex)
    {
        int startColumn = startIndex / _height;
        int endColumn = endIndex / _height;

        for (int column = startColumn; ; column++)
        {
            int columnX = column >> 4 & 15;
            int columnZ = column & 15;
            int columnBaseIndex = column * _height;
            int bottomY = unchecked(Math.Max(unchecked(startIndex - columnBaseIndex), 0) + _minY);
            int topY = unchecked(Math.Min(unchecked(endIndex - columnBaseIndex), _height - 1) + _minY);
            visitor(columnX, columnZ, bottomY, topY);

            if (column == endColumn)
                break;
        }
    }
}
