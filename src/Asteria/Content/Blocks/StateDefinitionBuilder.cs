using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects block properties and defaults before all possible states are materialized.
/// </summary>
public sealed class StateDefinitionBuilder
{
    private readonly List<StateProperty> _properties = new();
    private readonly Dictionary<StateProperty, object> _defaultValues =
        new(ReferenceEqualityComparer.Instance);
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

        if (property.ValueCount <= 1)
        {
            throw new ArgumentException(
                $"State property '{property.Name}' must have more than one possible value.",
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

    public StateDefinitionBuilder SetDefault<T>(StateProperty<T> property, T value) where T : notnull
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(property);

        if (!_properties.Any(existing => ReferenceEquals(existing, property)))
        {
            throw new InvalidOperationException(
                $"State property '{property.Name}' must be added before setting its default value.");
        }

        if (property.IndexOf(value) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Value is not valid for state property '{property.Name}'.");
        }

        _defaultValues[property] = value;
        return this;
    }

    internal StateDefinition Build(Block owner)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(owner);

        _built = true;
        return new StateDefinition(owner, _properties, _defaultValues);
    }

    private void EnsureMutable()
    {
        if (_built)
        {
            throw new InvalidOperationException("A state definition builder can only be used once.");
        }
    }
}
