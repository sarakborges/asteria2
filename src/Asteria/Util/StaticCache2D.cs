using System.Globalization;

namespace Asteria.Util;

/// <summary>
/// A fixed two-dimensional cache whose entries are initialized for every coordinate
/// in its rectangular bounds. Mirrors Minecraft Java 26.3 StaticCache2D.
/// </summary>
public class StaticCache2D<T>
{
    private readonly int _minX;
    private readonly int _minZ;
    private readonly int _sizeX;
    private readonly int _sizeZ;
    private readonly T[] _cache;

    public delegate T Initializer(int x, int z);

    public delegate U MappingFunction<U>(T value, int x, int z);

    private StaticCache2D(
        int minX,
        int minZ,
        int sizeX,
        int sizeZ,
        Initializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);

        _minX = minX;
        _minZ = minZ;
        _sizeX = sizeX;
        _sizeZ = sizeZ;
        _cache = new T[unchecked(_sizeX * _sizeZ)];

        for (int x = minX; x < unchecked(minX + sizeX); x++)
        {
            for (int z = minZ; z < unchecked(minZ + sizeZ); z++)
                _cache[GetIndex(x, z)] = initializer(x, z);
        }
    }

    public static StaticCache2D<T> Create(
        int centerX,
        int centerZ,
        int range,
        Initializer initializer)
    {
        int minX = unchecked(centerX - range);
        int minZ = unchecked(centerZ - range);
        int size = unchecked(2 * range + 1);
        return new StaticCache2D<T>(minX, minZ, size, size, initializer);
    }

    public StaticCache2D<U> Map<U>(MappingFunction<U> function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return new StaticCache2D<U>(
            _minX,
            _minZ,
            _sizeX,
            _sizeZ,
            (x, z) => function(Get(x, z), x, z));
    }

    public void ForEach(Action<T> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        foreach (T value in _cache)
            consumer(value);
    }

    public T Get(int x, int z)
    {
        if (!Contains(x, z))
        {
            throw new ArgumentException(
                $"Requested out of range value ({x},{z}) from {this}");
        }

        return _cache[GetIndex(x, z)];
    }

    public bool Contains(int x, int z)
    {
        int deltaX = unchecked(x - _minX);
        int deltaZ = unchecked(z - _minZ);
        return deltaX >= 0 && deltaX < _sizeX && deltaZ >= 0 && deltaZ < _sizeZ;
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"StaticCache2D[{_minX}, {_minZ}, {unchecked(_minX + _sizeX)}, {unchecked(_minZ + _sizeZ)}]");

    private int GetIndex(int x, int z)
    {
        int deltaX = unchecked(x - _minX);
        int deltaZ = unchecked(z - _minZ);
        return unchecked(deltaX * _sizeZ + deltaZ);
    }
}
