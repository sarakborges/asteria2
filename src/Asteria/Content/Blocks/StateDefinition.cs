using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// StateDefinition equivalent: sorted properties, Cartesian-product states, and cached neighbor tables.
/// </summary>
public sealed class StateDefinition
{
    private static readonly object Wildcard = new();

    private readonly StateProperty[] _properties;
    private readonly IReadOnlyList<StateProperty> _readOnlyProperties;
    private readonly BlockState[] _states;
    private readonly IReadOnlyList<BlockState> _readOnlyStates;
    private readonly Dictionary<StateProperty, int> _propertyIndices =
        new(ReferenceEqualityComparer.Instance);

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

        _properties = properties.OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        _readOnlyProperties = Array.AsReadOnly(_properties);

        long stateCount = 1;
        for (int index = _properties.Length - 1; index >= 0; index--)
        {
            StateProperty property = _properties[index];
            if (property.ValueCount <= 1)
            {
                throw new InvalidOperationException(
                    $"State property '{property.Name}' must have more than one possible value.");
            }

            stateCount = checked(stateCount * property.ValueCount);
            if (stateCount > Array.MaxLength)
            {
                throw new InvalidOperationException(
                    $"Block '{owner}' has too many possible states to materialize.");
            }
        }

        for (int index = 0; index < _properties.Length; index++)
        {
            _propertyIndices.Add(_properties[index], index);
        }

        _states = new BlockState[(int)stateCount];
        var statesByValues = new Dictionary<object[], BlockState>(ObjectArrayComparer.Instance);

        for (int stateIndex = 0; stateIndex < _states.Length; stateIndex++)
        {
            var values = new object[_properties.Length];
            int divisor = 1;

            for (int propertyIndex = _properties.Length - 1; propertyIndex >= 0; propertyIndex--)
            {
                StateProperty property = _properties[propertyIndex];
                int valueIndex = (stateIndex / divisor) % property.ValueCount;
                values[propertyIndex] = property.GetValueObject(valueIndex);
                divisor = checked(divisor * property.ValueCount);
            }

            var state = new BlockState(this, values);
            _states[stateIndex] = state;
            statesByValues.Add(values, state);
        }

        InitializeNeighbors(statesByValues);

        int defaultStateIndex = 0;
        int defaultStride = 1;
        for (int propertyIndex = _properties.Length - 1; propertyIndex >= 0; propertyIndex--)
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

                defaultStateIndex = checked(defaultStateIndex + valueIndex * defaultStride);
            }

            defaultStride = checked(defaultStride * property.ValueCount);
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

    private void InitializeNeighbors(Dictionary<object[], BlockState> statesByValues)
    {
        var neighborsByPivot = new Dictionary<object[], BlockState[]>(ObjectArrayComparer.Instance);

        foreach (BlockState state in _states)
        {
            var neighbors = new BlockState[_properties.Length];

            for (int propertyIndex = 0; propertyIndex < _properties.Length; propertyIndex++)
            {
                StateProperty property = _properties[propertyIndex];
                object[] pivotKey = state.CopyValues();
                pivotKey[propertyIndex] = Wildcard;

                if (!neighborsByPivot.TryGetValue(pivotKey, out BlockState[]? propertyNeighbors))
                {
                    propertyNeighbors = new BlockState[property.ValueCount];
                    object[] lookupValues = state.CopyValues();

                    for (int valueIndex = 0; valueIndex < property.ValueCount; valueIndex++)
                    {
                        lookupValues[propertyIndex] = property.GetValueObject(valueIndex);
                        if (!statesByValues.TryGetValue(lookupValues, out BlockState? neighbor))
                        {
                            throw new InvalidOperationException(
                                $"Could not resolve neighboring state for property '{property.Name}'.");
                        }

                        propertyNeighbors[valueIndex] = neighbor;
                    }

                    neighborsByPivot.Add(pivotKey, propertyNeighbors);
                }

                neighbors[propertyIndex] = propertyNeighbors;
            }

            state.InitializeNeighbors(neighbors);
        }
    }

    private sealed class ObjectArrayComparer : IEqualityComparer<object[]>
    {
        public static ObjectArrayComparer Instance { get; } = new();

        public bool Equals(object[]? left, object[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!object.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object[] values)
        {
            var hash = new HashCode();
            foreach (object value in values)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
