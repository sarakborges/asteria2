using System.Collections;
using System.Runtime.CompilerServices;
using Asteria.Content.Registries;

namespace Asteria.Content.Storage;

/// <summary>
/// Identity-keyed incremental value-to-ID map equivalent to
/// the reference incremental identity hash map.
/// </summary>
public sealed class CrudeIncrementalIntIdentityHashBiMap<T> : IIdMap<T> where T : class
{
    private const float LoadFactor = 0.8f;

    private T?[] _keys;
    private int[] _values;
    private T?[] _byId;
    private int _nextId;
    private int _size;

    private CrudeIncrementalIntIdentityHashBiMap(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _keys = new T?[capacity];
        _values = new int[capacity];
        _byId = new T?[capacity];
    }

    private CrudeIncrementalIntIdentityHashBiMap(
        T?[] keys,
        int[] values,
        T?[] byId,
        int nextId,
        int size)
    {
        _keys = keys;
        _values = values;
        _byId = byId;
        _nextId = nextId;
        _size = size;
    }

    public int Count => _size;

    public static CrudeIncrementalIntIdentityHashBiMap<T> Create(int initialCapacity)
    {
        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        int capacity = (int)(initialCapacity / LoadFactor);
        return new CrudeIncrementalIntIdentityHashBiMap<T>(capacity);
    }

    public int GetId(T? value) =>
        value is null ? -1 : GetValue(IndexOf(value, Hash(value)));

    public T? ById(int id) =>
        (uint)id < (uint)_byId.Length ? _byId[id] : null;

    public bool Contains(T? value) => GetId(value) != -1;

    public bool Contains(int id) => ById(id) is not null;

    public int Add(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int id = NextId();
        AddMapping(value, id);
        return id;
    }

    public void AddMapping(T value, int id)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        int minimumSize = Math.Max(id, _size + 1);
        if (minimumSize >= _keys.Length * LoadFactor)
        {
            int newSize = _keys.Length << 1;
            while (newSize < id)
            {
                newSize <<= 1;
            }

            Grow(newSize);
        }

        int index = FindEmpty(Hash(value));
        _keys[index] = value;
        _values[index] = id;
        _byId[id] = value;
        _size++;

        if (id == _nextId)
        {
            _nextId++;
        }
    }

    public void Clear()
    {
        Array.Clear(_keys);
        Array.Clear(_byId);
        _nextId = 0;
        _size = 0;
    }

    public List<T> GetEntries()
    {
        var entries = new List<T>(_size);
        foreach (T? value in _byId)
        {
            if (value is not null)
            {
                entries.Add(value);
            }
        }

        return entries;
    }

    public CrudeIncrementalIntIdentityHashBiMap<T> Copy() =>
        new(
            (T?[])_keys.Clone(),
            (int[])_values.Clone(),
            (T?[])_byId.Clone(),
            _nextId,
            _size);

    public IEnumerator<T> GetEnumerator()
    {
        foreach (T? value in _byId)
        {
            if (value is not null)
            {
                yield return value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int NextId()
    {
        while (_nextId < _byId.Length && _byId[_nextId] is not null)
        {
            _nextId++;
        }

        return _nextId;
    }

    private void Grow(int newSize)
    {
        T?[] oldKeys = _keys;
        int[] oldValues = _values;

        var resized = new CrudeIncrementalIntIdentityHashBiMap<T>(newSize);
        for (int index = 0; index < oldKeys.Length; index++)
        {
            if (oldKeys[index] is T key)
            {
                resized.AddMapping(key, oldValues[index]);
            }
        }

        _keys = resized._keys;
        _values = resized._values;
        _byId = resized._byId;
        _nextId = resized._nextId;
        _size = resized._size;
    }

    private int Hash(T value)
    {
        uint hash = (uint)RuntimeHelpers.GetHashCode(value);
        hash ^= hash >> 16;
        hash *= unchecked((uint)-2048144789);
        hash ^= hash >> 13;
        hash *= unchecked((uint)-1028477387);
        hash ^= hash >> 16;

        return (int)(hash & 0x7FFFFFFF) % _keys.Length;
    }

    private int IndexOf(T value, int startFrom)
    {
        for (int index = startFrom; index < _keys.Length; index++)
        {
            if (ReferenceEquals(_keys[index], value))
            {
                return index;
            }

            if (_keys[index] is null)
            {
                return -1;
            }
        }

        for (int index = 0; index < startFrom; index++)
        {
            if (ReferenceEquals(_keys[index], value))
            {
                return index;
            }

            if (_keys[index] is null)
            {
                return -1;
            }
        }

        return -1;
    }

    private int FindEmpty(int startFrom)
    {
        for (int index = startFrom; index < _keys.Length; index++)
        {
            if (_keys[index] is null)
            {
                return index;
            }
        }

        for (int index = 0; index < startFrom; index++)
        {
            if (_keys[index] is null)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Identity ID map overflowed.");
    }

    private int GetValue(int index) => index == -1 ? -1 : _values[index];
}
