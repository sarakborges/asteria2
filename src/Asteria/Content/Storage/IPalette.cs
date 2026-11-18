namespace Asteria.Content.Storage;

internal interface IPalette<T> where T : class
{
    int Count { get; }
    int IdFor(T value, IPaletteResize<T> resize);
    T ValueFor(int id);
    bool MaybeHas(Func<T, bool> predicate);
    void ForEach(Action<T> action);
    IPalette<T> Copy();
}
