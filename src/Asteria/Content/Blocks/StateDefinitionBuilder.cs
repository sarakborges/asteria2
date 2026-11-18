using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Collects a block's state properties and default values before state combinations are materialized.
/// </summary>
public sealed class StateDefinitionBuilder
{
    private readonly List<StateProperty> _properties = new();
    private readonly Dictionary<StateProperty, object> _defaultValues =
        new(ReferenceEqualityComparer.Instance);
    private bool _built;

    public StateDefinitionBuilder Add<T>(StateProperty<T> property) where T : notnull
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(property);

        if (property.ValueCount == 0)
        {
            throw new ArgumentException(
                $"State property '{property.Name}' has no possible values.",
                nameof(property));
        }

        if (_properties.Any(existing =>
                ReferenceEquals(existing, property) ||
                string.Equals(existing.Name, property.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"State property '{property.Name}' has already been added to this block.");
        }

        _properties.Add(property);
        return this;
    }

    public StateDefinitionBuilder SetDefault<T>(
        StateProperty<T> property,
        T value) where T : notnull
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
