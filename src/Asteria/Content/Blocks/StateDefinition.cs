using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// Contains every valid immutable state for one block and provides fast transitions between them.
/// Properties are ordered by name, and each property's declared value order is preserved.
/// </summary>
public sealed class StateDefinition
{
    private readonly StateProperty[] _properties;
    private readonly IReadOnlyList<StateProperty> _readOnlyProperties;
    private readonly BlockState[] _states;
    private readonly IReadOnlyList<BlockState> _readOnlyStates;
    private readonly Dictionary<StateProperty, int> _propertyIndices =
        new(ReferenceEqualityComparer.Instance);
    private readonly int[] _strides;

    public Block Owner { get; }
    public IReadOnlyList<StateProperty> Properties => _readOnlyProperties;
    public IReadOnlyList<BlockState> PossibleStates => _readOnlyStates;
    public BlockState DefaultState { get; }

    internal StateDefinition(
        Block owner,
        IReadOnlyList<StateProperty> properties,
        IReadOnlyDictionary<StateProperty, object> defaultValues)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(defaultValues);

        _properties = properties
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        _readOnlyProperties = Array.AsReadOnly(_properties);
        _strides = new int[_properties.Length];

        long stateCount = 1L;
        for (int index = _properties.Length - 1; index >= 0; index--)
        {
            StateProperty property = _properties[index];
            if (property.ValueCount <= 0)
            {
                throw new InvalidOperationException(
                    $"State property '{property.Name}' must have at least one possible value.");
            }

            if (stateCount > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Block '{owner}' has too many possible states to index.");
            }

            _strides[index] = (int)stateCount;
            stateCount = checked(stateCount * property.ValueCount);
        }

        if (stateCount > Array.MaxLength)
        {
            throw new InvalidOperationException(
                $"Block '{owner}' has {stateCount} possible states, exceeding the supported array size.");
        }

        for (int index = 0; index < _properties.Length; index++)
        {
            _propertyIndices.Add(_properties[index], index);
        }

        _states = new BlockState[(int)stateCount];
        for (int stateIndex = 0; stateIndex < _states.Length; stateIndex++)
        {
            var values = new object[_properties.Length];

            for (int propertyIndex = 0; propertyIndex < _properties.Length; propertyIndex++)
            {
                StateProperty property = _properties[propertyIndex];
                int valueIndex = (stateIndex / _strides[propertyIndex]) % property.ValueCount;
                values[propertyIndex] = property.GetValueObject(valueIndex);
            }

            _states[stateIndex] = new BlockState(this, stateIndex, values);
        }

        int defaultStateIndex = 0;
        for (int propertyIndex = 0; propertyIndex < _properties.Length; propertyIndex++)
        {
            StateProperty property = _properties[propertyIndex];
            if (defaultValues.TryGetValue(property, out object? defaultValue))
            {
                int valueIndex = property.GetValueIndex(defaultValue);
                if (valueIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Default value '{defaultValue}' is invalid for state property '{property.Name}'.");
                }

                defaultStateIndex += valueIndex * _strides[propertyIndex];
            }
        }

        DefaultState = _states[defaultStateIndex];
        _readOnlyStates = Array.AsReadOnly(_states);
    }

    public BlockState Any() => _states[0];

    public StateProperty? GetProperty(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _properties.FirstOrDefault(
            property => string.Equals(property.Name, name, StringComparison.Ordinal));
    }

    public bool HasProperty(StateProperty property) =>
        property is not null && _propertyIndices.ContainsKey(property);

    internal int GetPropertyIndex(StateProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (_propertyIndices.TryGetValue(property, out int index))
        {
            return index;
        }

        throw new ArgumentException(
            $"Property '{property.Name}' does not belong to block '{Owner}'.",
            nameof(property));
    }

    internal bool TryGetPropertyIndex(StateProperty property, out int index) =>
        _propertyIndices.TryGetValue(property, out index);

    internal BlockState WithValue<T>(
        BlockState state,
        StateProperty<T> property,
        T value) where T : notnull
    {
        int propertyIndex = GetPropertyIndex(property);
        int valueIndex = property.IndexOf(value);

        if (valueIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Value is not valid for state property '{property.Name}'.");
        }

        int stride = _strides[propertyIndex];
        int currentValueIndex =
            (state.StateIndex / stride) % property.ValueCount;
        int targetStateIndex =
            state.StateIndex + ((valueIndex - currentValueIndex) * stride);

        return _states[targetStateIndex];
    }

    internal bool TryWithValue<T>(
        BlockState state,
        StateProperty<T> property,
        T value,
        out BlockState? result) where T : notnull
    {
        if (!TryGetPropertyIndex(property, out int propertyIndex))
        {
            result = null;
            return false;
        }

        int valueIndex = property.IndexOf(value);
        if (valueIndex < 0)
        {
            result = null;
            return false;
        }

        int stride = _strides[propertyIndex];
        int currentValueIndex =
            (state.StateIndex / stride) % property.ValueCount;
        int targetStateIndex =
            state.StateIndex + ((valueIndex - currentValueIndex) * stride);

        result = _states[targetStateIndex];
        return true;
    }

    internal BlockState CycleValue<T>(
        BlockState state,
        StateProperty<T> property) where T : notnull
    {
        int propertyIndex = GetPropertyIndex(property);
        int stride = _strides[propertyIndex];
        int currentValueIndex =
            (state.StateIndex / stride) % property.ValueCount;
        int nextValueIndex = (currentValueIndex + 1) % property.ValueCount;
        int targetStateIndex =
            state.StateIndex + ((nextValueIndex - currentValueIndex) * stride);

        return _states[targetStateIndex];
    }
}
