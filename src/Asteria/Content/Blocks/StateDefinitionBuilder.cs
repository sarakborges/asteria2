using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects block properties before all possible states are materialized.
/// </summary>
public sealed class StateDefinitionBuilder
{
    private readonly List<StateProperty> _properties = new();
    private bool _built;

    public StateDefinitionBuilder Add<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(property);

        if (!StateProperty.IsValidName(property.Name))
        {
            throw new ArgumentException(
                $"Invalid state property name '{property.Name}'. Names must use lowercase ASCII letters, digits, or underscores.",
                nameof(property));
        }

        if (property.ValueCount == 0)
        {
            throw new ArgumentException(
                $"State property '{property.Name}' must have at least one possible value.",
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
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(owner);

        _built = true;
        return new StateDefinition(owner, _properties);
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("A state definition builder can only be used once.");
        }
    }
}
