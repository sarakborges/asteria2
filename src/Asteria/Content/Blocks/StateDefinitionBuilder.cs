using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects block properties before all possible states are materialized.
/// </summary>
public sealed class StateDefinitionBuilder
{
    private readonly List<StateProperty> _properties = new();

    public StateDefinitionBuilder Add<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        ArgumentNullException.ThrowIfNull(property);

        if (!StateProperty.IsValidName(property.Name))
        {
            throw new ArgumentException(
                $"Invalid state property name '{property.Name}'. Names must use lowercase ASCII letters, digits, or underscores.",
                nameof(property));
        }

        if (property.ValueCount <= 1)
        {
            throw new ArgumentException(
                $"State definition cannot use property '{property.Name}' with one or fewer possible values.",
                nameof(property));
        }

        for (int index = 0; index < property.ValueCount; index++)
        {
            string serializedValue = property.GetSerializedValue(property.GetValueObject(index));
            if (!StateProperty.IsValidName(serializedValue))
            {
                throw new ArgumentException(
                    $"State property '{property.Name}' has an invalid serialized value '{serializedValue}'.",
                    nameof(property));
            }
        }

        if (_properties.Any(existing =>
                string.Equals(existing.Name, property.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"State property '{property.Name}' has already been added to this block.",
                nameof(property));
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
