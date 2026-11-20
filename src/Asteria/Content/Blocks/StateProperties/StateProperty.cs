namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// Non-generic base for block-state properties.
/// </summary>
public abstract class StateProperty
{
    public string Name { get; }
    protected abstract Type ValueType { get; }
    public abstract int ValueCount { get; }

    protected StateProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    internal abstract object GetValueObject(int index);
    internal abstract int GetValueIndex(object value);
    internal abstract string GetSerializedValue(object value);

    public override bool Equals(object? obj) =>
        obj is StateProperty other &&
        ValueType == other.ValueType &&
        string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override int GetHashCode() =>
        HashCode.Combine(ValueType, Name);

    internal static bool IsValidName(string value)
    {
        foreach (char character in value)
        {
            if (!(character is >= 'a' and <= 'z') &&
                !(character is >= '0' and <= '9') &&
                character != '_')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    protected static void ValidateSerializedName(string value, string parameterName)
    {
        if (!IsValidName(value))
        {
            throw new ArgumentException(
                $"Invalid serialized property value '{value}'. Values must use lowercase ASCII letters, digits, or underscores.",
                parameterName);
        }
    }
}

/// <summary>
/// A typed property with a finite, ordered set of allowed values.
/// </summary>
public abstract class StateProperty<T> : StateProperty where T : notnull, IComparable
{
    protected override Type ValueType => typeof(T);

    public abstract IReadOnlyList<T> PossibleValues { get; }

    protected StateProperty(string name) : base(name)
    {
    }

    public abstract string GetName(T value);
    public abstract bool TryParse(string serializedValue, out T value);

    public virtual int IndexOf(T value)
    {
        for (int index = 0; index < PossibleValues.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(PossibleValues[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    protected int GetPossibleValuesHashCode()
    {
        int hash = 1;
        foreach (T value in PossibleValues)
        {
            hash = unchecked(hash * 31 + EqualityComparer<T>.Default.GetHashCode(value));
        }

        return hash;
    }

    internal override object GetValueObject(int index) => PossibleValues[index];

    internal override int GetValueIndex(object value) =>
        value is T typedValue ? IndexOf(typedValue) : -1;

    internal override string GetSerializedValue(object value)
    {
        if (value is not T typedValue || IndexOf(typedValue) < 0)
        {
            throw new ArgumentException(
                $"Value '{value}' is not valid for property '{Name}'.",
                nameof(value));
        }

        return GetName(typedValue);
    }
}
