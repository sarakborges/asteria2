namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// A property backed by a selected set of enum values.
/// Values are ordered by the enum's underlying numeric value. Serialized values default
/// to the lowercase enum member name; a selector can override them.
/// </summary>
public sealed class EnumProperty<TEnum> : StateProperty<TEnum>
    where TEnum : struct, Enum
{
    private readonly IReadOnlyList<TEnum> _values;
    private readonly Dictionary<TEnum, string> _namesByValue;
    private readonly Dictionary<string, TEnum> _valuesByName;

    public override IReadOnlyList<TEnum> PossibleValues => _values;
    public override int ValueCount => _values.Count;

    private EnumProperty(
        string name,
        IEnumerable<TEnum> values,
        Func<TEnum, string> nameSelector) : base(name)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(nameSelector);

        var orderedValues = new List<TEnum>();
        _namesByValue = new Dictionary<TEnum, string>();
        _valuesByName = new Dictionary<string, TEnum>(StringComparer.Ordinal);

        foreach (TEnum value in values.OrderBy(value => value, Comparer<TEnum>.Default))
        {
            if (_namesByValue.ContainsKey(value))
            {
                throw new ArgumentException(
                    $"Enum value '{value}' was provided more than once for property '{name}'.",
                    nameof(values));
            }

            string serializedName = nameSelector(value);
            ValidateSerializedName(serializedName, nameof(nameSelector));

            if (_valuesByName.ContainsKey(serializedName))
            {
                throw new ArgumentException(
                    $"Serialized enum value '{serializedName}' is duplicated for property '{name}'.",
                    nameof(nameSelector));
            }

            orderedValues.Add(value);
            _namesByValue.Add(value, serializedName);
            _valuesByName.Add(serializedName, value);
        }

        if (orderedValues.Count == 0)
        {
            throw new ArgumentException("An enum property must contain at least one value.", nameof(values));
        }

        _values = orderedValues.AsReadOnly();
    }

    public static EnumProperty<TEnum> Create(string name)
    {
        TEnum[] values = Enum.GetValues<TEnum>();
        return new EnumProperty<TEnum>(
            name,
            values,
            static value => value.ToString().ToLowerInvariant());
    }

    public static EnumProperty<TEnum> Create(
        string name,
        IEnumerable<TEnum> possibleValues,
        Func<TEnum, string>? nameSelector = null)
    {
        return new EnumProperty<TEnum>(
            name,
            possibleValues,
            nameSelector ?? (static value => value.ToString().ToLowerInvariant()));
    }

    public override string GetName(TEnum value)
    {
        if (_namesByValue.TryGetValue(value, out string? name))
        {
            return name;
        }

        throw new ArgumentOutOfRangeException(
            nameof(value), value, $"Value is not valid for '{Name}'.");
    }

    public override bool TryParse(string serializedValue, out TEnum value) =>
        _valuesByName.TryGetValue(serializedValue, out value);
}
