using Asteria.Content.Registries;

namespace Asteria.Serialization;

/// <summary>
/// DynamicOps decorator carrying the registry lookup context used by registry-aware codecs.
/// </summary>
public sealed class RegistryOps<T> : DynamicOps<T>
{
    private readonly DynamicOps<T> _parent;

    public HolderLookupProvider LookupProvider { get; }

    public bool CompressMaps => _parent.CompressMaps;
    public T Empty => _parent.Empty;

    private RegistryOps(
        DynamicOps<T> parent,
        HolderLookupProvider lookupProvider)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        LookupProvider = lookupProvider ?? throw new ArgumentNullException(nameof(lookupProvider));
    }

    public static RegistryOps<T> Create(
        DynamicOps<T> parent,
        HolderLookupProvider lookupProvider) =>
        new(parent, lookupProvider);

    public HolderLookup<TElement>.RegistryLookup? Lookup<TElement>(
        RegistryKey<Registry<TElement>> key)
        where TElement : class =>
        LookupProvider.Lookup<TElement>(key);

    public bool IsEmpty(T input) =>
        _parent.IsEmpty(input);

    public DataResult<NumberValue> GetNumberValue(T input) =>
        _parent.GetNumberValue(input);

    public T CreateNumeric(NumberValue value) =>
        _parent.CreateNumeric(value);

    public DataResult<bool> GetBooleanValue(T input) =>
        _parent.GetBooleanValue(input);

    public T CreateBoolean(bool value) =>
        _parent.CreateBoolean(value);

    public DataResult<string> GetStringValue(T input) =>
        _parent.GetStringValue(input);

    public T CreateString(string value) =>
        _parent.CreateString(value);

    public DataResult<IReadOnlyList<KeyValuePair<T, T>>> GetMapValues(T input) =>
        _parent.GetMapValues(input);

    public T CreateMap(IEnumerable<KeyValuePair<T, T>> values) =>
        _parent.CreateMap(values);

    public DataResult<IReadOnlyList<T>> GetList(T input) =>
        _parent.GetList(input);

    public T CreateList(IEnumerable<T> values) =>
        _parent.CreateList(values);

    public DataResult<T> Get(T input, string key) =>
        _parent.Get(input, key);

    public DataResult<T> MergeToList(T list, T value) =>
        _parent.MergeToList(list, value);

    public DataResult<T> MergeToList(T list, IReadOnlyList<T> values) =>
        _parent.MergeToList(list, values);

    public DataResult<T> MergeToMap(T map, T key, T value) =>
        _parent.MergeToMap(map, key, value);

    public DataResult<T> MergeToMap(T map, IReadOnlyList<KeyValuePair<T, T>> values) =>
        _parent.MergeToMap(map, values);

    public T Remove(T input, string key) =>
        _parent.Remove(input, key);
}
