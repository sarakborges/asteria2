using Asteria.Content.Registries;

namespace Asteria.Content.Storage;

/// <summary>
/// Direct palette backed by the global ID map.
/// </summary>
internal sealed class GlobalPalette<T> : IPalette<T> where T : class
{
    private readonly IIdMap<T> _idMap;

    public int Count => _idMap.Count;

    public GlobalPalette(IIdMap<T> idMap)
    {
        _idMap = idMap ?? throw new ArgumentNullException(nameof(idMap));
    }

    public int IdFor(T value, IPaletteResize<T> resize)
    {
        int id = _idMap.GetId(value);
        return id == -1 ? 0 : id;
    }

    public T ValueFor(int id) =>
        _idMap.ById(id) ?? throw new MissingPaletteEntryException(id);

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
            action(_idMap.ByIdOrThrow(id));
        }
    }

    public IPalette<T> Copy() => this;
}
