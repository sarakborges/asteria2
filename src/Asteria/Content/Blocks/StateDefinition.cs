using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

public class StateDefinition<TOwner, TState>
    where TOwner : notnull
    where TState : StateHolder<TOwner, TState>
{
    private static readonly object Wildcard = new();
    private readonly StateProperty[] _properties;
    private readonly IReadOnlyList<StateProperty> _readOnlyProperties;
    private readonly TState[] _states;
    private readonly IReadOnlyList<TState> _readOnlyStates;
    private readonly Dictionary<StateProperty, int> _propertyIndices = new(ReferenceEqualityComparer.Instance);

    public TOwner Owner { get; }
    public IReadOnlyList<StateProperty> Properties => _readOnlyProperties;
    public IReadOnlyList<TState> PossibleStates => _readOnlyStates;
    public bool IsSingletonState => _properties.Length == 0;

    public StateDefinition(
        TOwner owner,
        IReadOnlyList<StateProperty> properties,
        Func<StateDefinition<TOwner, TState>, object[], TState> stateFactory)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(stateFactory);

        Owner = owner;
        _properties = properties.OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
        _readOnlyProperties = Array.AsReadOnly(_properties);

        long stateCount = 1;
        for (int index = _properties.Length - 1; index >= 0; index--)
        {
            StateProperty property = _properties[index];
            if (property.ValueCount == 0)
                throw new InvalidOperationException($"State property '{property.Name}' must have at least one possible value.");

            stateCount = checked(stateCount * property.ValueCount);
            if (stateCount > Array.MaxLength)
                throw new InvalidOperationException($"'{owner}' has too many possible states to materialize.");
        }

        for (int index = 0; index < _properties.Length; index++)
            _propertyIndices.Add(_properties[index], index);

        _states = new TState[(int)stateCount];
        var statesByValues = new Dictionary<object[], TState>(ObjectArrayComparer.Instance);

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

            TState state = stateFactory(this, values);
            _states[stateIndex] = state;
            statesByValues.Add(values, state);
        }

        InitializeNeighbors(statesByValues);
        _readOnlyStates = Array.AsReadOnly(_states);
    }

    public TState Any() => _states[0];

    public StateProperty? GetProperty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.Ordinal));
    }

    public bool HasProperty(StateProperty property) =>
        property is not null && _propertyIndices.ContainsKey(property);

    internal bool TryGetPropertyIndex(StateProperty? property, out int index)
    {
        if (property is not null && _propertyIndices.TryGetValue(property, out index))
            return true;
        index = -1;
        return false;
    }

    private void InitializeNeighbors(Dictionary<object[], TState> statesByValues)
    {
        var neighborsByPivot = new Dictionary<object[], TState[]>(ObjectArrayComparer.Instance);
        foreach (TState state in _states)
        {
            var neighbors = new TState[_properties.Length][];
            for (int propertyIndex = 0; propertyIndex < _properties.Length; propertyIndex++)
            {
                StateProperty property = _properties[propertyIndex];
                object[] pivotKey = state.CopyValues();
                pivotKey[propertyIndex] = Wildcard;

                if (!neighborsByPivot.TryGetValue(pivotKey, out TState[]? propertyNeighbors))
                {
                    propertyNeighbors = new TState[property.ValueCount];
                    object[] lookupValues = state.CopyValues();
                    for (int valueIndex = 0; valueIndex < property.ValueCount; valueIndex++)
                    {
                        lookupValues[propertyIndex] = property.GetValueObject(valueIndex);
                        if (!statesByValues.TryGetValue(lookupValues, out TState? neighbor))
                            throw new InvalidOperationException($"Could not resolve neighboring state for property '{property.Name}'.");
                        propertyNeighbors[valueIndex] = neighbor;
                    }

                    neighborsByPivot.Add(pivotKey, propertyNeighbors);
                }

                neighbors[propertyIndex] = propertyNeighbors;
            }

            state.InitializeNeighbors(neighbors);
        }
    }

    public override string ToString() =>
        $"StateDefinition{{owner={Owner}, properties=[{string.Join(", ", _properties.Select(property => property.Name))}]}}";

    private sealed class ObjectArrayComparer : IEqualityComparer<object[]>
    {
        public static ObjectArrayComparer Instance { get; } = new();

        public bool Equals(object[]? left, object[]? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (!object.Equals(left[index], right[index]))
                    return false;
            }
            return true;
        }

        public int GetHashCode(object[] values)
        {
            var hash = new HashCode();
            foreach (object value in values)
                hash.Add(value);
            return hash.ToHashCode();
        }
    }
}

public sealed class StateDefinition : StateDefinition<Block, BlockState>
{
    internal StateDefinition(Block owner, IReadOnlyList<StateProperty> properties)
        : base(owner, properties, static (definition, values) => new BlockState((StateDefinition)definition, values))
    {
    }

    public override string ToString() =>
        $"StateDefinition{{block={Owner}, properties=[{string.Join(", ", Properties.Select(property => property.Name))}]}}";
}
