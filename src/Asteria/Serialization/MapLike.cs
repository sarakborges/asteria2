namespace Asteria.Serialization;

/// <summary>
/// Read-only view over a serialized map, retaining the source value and its format operations.
/// </summary>
public sealed class MapLike<T>
{
    public T Input { get; }
    public IReadOnlyList<KeyValuePair<T, T>> Entries { get; }

    private readonly DynamicOps<T> _ops;

    public MapLike(
        DynamicOps<T> ops,
        T input,
        IReadOnlyList<KeyValuePair<T, T>> entries)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        Input = input;
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public DataResult<T> Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _ops.Get(Input, key);
    }

    public DataResult<T> Get(T key) =>
        _ops.GetStringValue(key).FlatMap(name => _ops.Get(Input, name));

    public override string ToString() =>
        $"MapLike[{Input}]";
}
