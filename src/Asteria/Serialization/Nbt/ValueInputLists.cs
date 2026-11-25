namespace Asteria.Serialization.Nbt;

/// <summary>Read-only sequence of child compound inputs.</summary>
public sealed class ValueInputList : IReadOnlyList<TagValueInput>
{
    private static readonly ValueInputList EmptyValue = new(Array.Empty<TagValueInput>());
    private readonly IReadOnlyList<TagValueInput> _values;

    public static ValueInputList Empty => EmptyValue;

    public ValueInputList(IEnumerable<TagValueInput> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = Array.AsReadOnly(values.ToArray());
    }

    public TagValueInput this[int index] => _values[index];

    public int Count => _values.Count;

    public bool IsEmpty() => _values.Count == 0;

    public IEnumerator<TagValueInput> GetEnumerator() => _values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}

/// <summary>Read-only sequence of values decoded from a list tag.</summary>
public sealed class TypedInputList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _values;

    public TypedInputList(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = Array.AsReadOnly(values.ToArray());
    }

    public T this[int index] => _values[index];

    public int Count => _values.Count;

    public bool IsEmpty() => _values.Count == 0;

    public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
