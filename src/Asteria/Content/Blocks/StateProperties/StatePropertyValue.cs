using System.Globalization;

namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// Non-generic view of a typed block-state property value.
/// </summary>
public interface IStatePropertyValue
{
    StateProperty Property { get; }
    object ValueObject { get; }
    string ValueName { get; }
}

/// <summary>
/// Typed property/value pair used by block-state definitions.
/// </summary>
public readonly record struct StatePropertyValue<T> : IStatePropertyValue
    where T : notnull, IComparable
{
    public StateProperty<T> Property { get; }
    public T Value { get; }

    StateProperty IStatePropertyValue.Property => Property;
    object IStatePropertyValue.ValueObject => Value;

    public string ValueName => Property.GetName(Value);

    public StatePropertyValue(StateProperty<T> property, T value)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        if (!Property.PossibleValues.Contains(value))
        {
            throw new ArgumentException(
                $"Value {value} does not belong to property {Property}");
        }

        Value = value;
    }

    public override string ToString()
    {
        string valueText = Value switch
        {
            bool booleanValue => booleanValue ? "true" : "false",
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Value.ToString() ?? string.Empty
        };

        return $"Value[property={Property}, value={valueText}]";
    }
}
