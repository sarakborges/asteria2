using Asteria.Content.Registries;

namespace Asteria.Content.Storage;

/// <summary>
/// Direct palette backed by the frozen global ID map.
/// </summary>
internal sealed class GlobalPalette<T> : IPalette<T> where T : class
{
    private readonly IIdMap<T> _idMap;

    public int Count => _idMap.Count;

    public GlobalPalette(IIdMap<T> idMap)
    {
        _idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
        if (!_idMap.IsFrozen)
        {
            throw new InvalidOperationException("The global ID map must be frozen before creating a global palette.");
        }
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        int id = _idMap.GetId(value);
        return id == -1 ? 0 : id;
    }

    public T ValueFor(int id) => _idMap.GetById(id);

    public bool MaybeHas(Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return true;
    }

    public void ForEach(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        for (int id = 0; id < _idMap.Count; id++)
        {
            action(_idMap.GetById(id));
        }
    }

    public IPalette<T> Copy() => this;
}
