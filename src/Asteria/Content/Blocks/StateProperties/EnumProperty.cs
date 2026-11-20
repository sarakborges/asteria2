using System.Reflection;

namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// A property backed by a selected set of enum values.
/// Values retain their supplied order. Serialized values default to the lowercase enum
/// member name; a selector can override them.
/// </summary>
public sealed class EnumProperty<TEnum> : StateProperty<TEnum>
    where TEnum : struct, Enum
{
    private readonly IReadOnlyList<TEnum> _values;
    private readonly Dictionary<string, TEnum> _valuesByName;
    private readonly Dictionary<TEnum, int> _indicesByValue;
    private readonly Func<TEnum, string> _nameSelector;

    public override IReadOnlyList<TEnum> PossibleValues => _values;
    public override int ValueCount => _values.Count;

    private EnumProperty(
        string name,
        IEnumerable<TEnum> values,
        Func<TEnum, string> nameSelector) : base(name)
    {
        ArgumentNullException.ThrowIfNull(values);
        _nameSelector = nameSelector ?? throw new ArgumentNullException(nameof(nameSelector));

        var orderedValues = new List<TEnum>();
        _valuesByName = new Dictionary<string, TEnum>(StringComparer.Ordinal);
        _indicesByValue = new Dictionary<TEnum, int>();

        foreach (TEnum value in values)
        {
            if (_indicesByValue.ContainsKey(value))
            {
                throw new ArgumentException(
                    $"Enum value '{value}' was provided more than once for property '{name}'.",
                    nameof(values));
            }

            string serializedName = _nameSelector(value);

            if (_valuesByName.ContainsKey(serializedName))
            {
                throw new ArgumentException(
                    $"Serialized enum value '{serializedName}' is duplicated for property '{name}'.",
                    nameof(nameSelector));
            }

            _indicesByValue.Add(value, orderedValues.Count);
            orderedValues.Add(value);
            _valuesByName.Add(serializedName, value);
        }

        if (orderedValues.Count == 0)
        {
            throw new ArgumentException($"Trying to make empty EnumProperty '{name}'");
        }

        _values = orderedValues.AsReadOnly();
    }

    public override bool Equals(object? obj) =>
        obj is EnumProperty<TEnum> other &&
        base.Equals(other) &&
        _values.SequenceEqual(other._values);

    public override int GenerateHashCode() =>
        unchecked(base.GenerateHashCode() * 31 + GetPossibleValuesHashCode());

    public static EnumProperty<TEnum> Create(string name)
    {
        TEnum[] values = typeof(TEnum)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(field => field.MetadataToken)
            .Select(field => (TEnum)field.GetValue(null)!)
            .ToArray();
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

    public override int IndexOf(TEnum value) =>
        _indicesByValue.TryGetValue(value, out int index) ? index : -1;

    public override string GetName(TEnum value) =>
        _nameSelector(value);

    public override bool TryParse(string serializedValue, out TEnum value) =>
        _valuesByName.TryGetValue(serializedValue, out value);
}
