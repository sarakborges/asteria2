namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// Non-generic base for block-state properties.
/// </summary>
public abstract class StateProperty
{
    public string Name { get; }
    public abstract int ValueCount { get; }

    protected StateProperty(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!IsValidName(name))
        {
            throw new ArgumentException(
                $"Invalid state property name '{name}'. Names must use lowercase ASCII letters, digits, or underscores.",
                nameof(name));
        }

        Name = name;
    }

    internal abstract object GetValueObject(int index);
    internal abstract int GetValueIndex(object value);
    internal abstract string GetSerializedValue(object value);

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
public abstract class StateProperty<T> : StateProperty where T : notnull
{
    public abstract IReadOnlyList<T> PossibleValues { get; }

    protected StateProperty(string name) : base(name)
    {
    }

    public abstract string GetName(T value);
    public abstract bool TryParse(string serializedValue, out T value);

    public int IndexOf(T value)
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
