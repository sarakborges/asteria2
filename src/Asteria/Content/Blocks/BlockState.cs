using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// One precomputed immutable combination of a block's state properties.
/// Changing a property returns another precomputed state instance from the same definition.
/// </summary>
public sealed class BlockState
{
    private readonly object[] _values;
    private int _globalId = -1;

    public Block Block => Definition.Owner;
    public StateDefinition Definition { get; }
    public int GlobalId => _globalId;
    public bool HasGlobalId => _globalId >= 0;
    internal int StateIndex { get; }

    internal BlockState(StateDefinition definition, int stateIndex, object[] values)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        StateIndex = stateIndex;
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public T GetValue<T>(StateProperty<T> property) where T : notnull
    {
        int propertyIndex = Definition.GetPropertyIndex(property);
        return (T)_values[propertyIndex];
    }

    public bool TryGetValue<T>(StateProperty<T> property, out T value) where T : notnull
    {
        if (Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            value = (T)_values[propertyIndex];
            return true;
        }

        value = default!;
        return false;
    }

    public bool HasProperty(StateProperty property) => Definition.HasProperty(property);

    public BlockState SetValue<T>(StateProperty<T> property, T value) where T : notnull =>
        Definition.WithValue(this, property, value);

    public bool TrySetValue<T>(
        StateProperty<T> property,
        T value,
        out BlockState? result) where T : notnull =>
        Definition.TryWithValue(this, property, value, out result);

    public BlockState Cycle<T>(StateProperty<T> property) where T : notnull =>
        Definition.CycleValue(this, property);

    internal void AssignGlobalId(int globalId)
    {
        if (globalId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(globalId));
        }

        if (_globalId >= 0)
        {
            throw new InvalidOperationException("A block state can only receive one global runtime ID.");
        }

        _globalId = globalId;
    }

    public override string ToString()
    {
        if (_values.Length == 0)
        {
            return Block.ToString() ?? Block.GetType().Name;
        }

        var properties = new string[_values.Length];
        for (int index = 0; index < _values.Length; index++)
        {
            StateProperty property = Definition.Properties[index];
            properties[index] = $"{property.Name}={property.GetSerializedValue(_values[index])}";
        }

        return $"{Block}[{string.Join(",", properties)}]";
    }
}
