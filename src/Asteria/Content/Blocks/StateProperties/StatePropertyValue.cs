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
/// Typed equivalent of Minecraft's Property.Value record.
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

    public override string ToString() =>
        $"{Property.Name}={Property.GetName(Value)}";
}
