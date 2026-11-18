namespace Asteria.Content.Storage;

internal interface ILocalPalette<T> where T : class
{
    int Count { get; }
    IReadOnlyList<T> Values { get; }
    bool TryGetIndex(T value, out int index);
    T GetValue(int index);
    int Add(T value);
}
