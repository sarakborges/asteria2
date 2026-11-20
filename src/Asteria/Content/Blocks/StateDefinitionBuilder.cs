using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects block properties before all possible states are materialized.
/// </summary>
public sealed class StateDefinitionBuilder
{
    private readonly Block _owner;
    private readonly List<StateProperty> _properties = new();

    public StateDefinitionBuilder(Block owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public StateDefinitionBuilder Add<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        ArgumentNullException.ThrowIfNull(property);

        if (!StateProperty.IsValidName(property.Name))
        {
            throw new ArgumentException(
                $"{_owner} has invalidly named property: {property.Name}");
        }

        if (property.ValueCount <= 1)
        {
            throw new ArgumentException(
                $"{_owner} attempted use property {property.Name} with <= 1 possible values");
        }

        for (int index = 0; index < property.ValueCount; index++)
        {
            string serializedValue = property.GetSerializedValue(property.GetValueObject(index));
            if (!StateProperty.IsValidName(serializedValue))
            {
                throw new ArgumentException(
                    $"{_owner} has property: {property.Name} with invalidly named value: {serializedValue}");
            }
        }

        if (_properties.Any(existing =>
                string.Equals(existing.Name, property.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"{_owner} has duplicate property: {property.Name}");
        }

        _properties.Add(property);
        return this;
    }

    internal StateDefinition Build(Block owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new StateDefinition(owner, _properties);
    }
}
