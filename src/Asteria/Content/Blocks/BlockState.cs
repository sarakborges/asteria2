using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// StateHolder equivalent. Property transitions resolve through precomputed neighboring states.
/// </summary>
public sealed class BlockState
{
    private readonly object[] _values;
    private BlockState[][]? _neighbors;
    private int _globalId = -1;

    public Block Block => Definition.Owner;
    public StateDefinition Definition { get; }
    public int GlobalId => _globalId;
    public bool HasGlobalId => _globalId >= 0;

    internal BlockState(StateDefinition definition, object[] values)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
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

    public T GetValueOrElse<T>(StateProperty<T> property, T defaultValue) where T : notnull =>
        TryGetValue(property, out T value) ? value : defaultValue;

    public bool HasProperty(StateProperty property) => Definition.HasProperty(property);

    public BlockState SetValue<T>(StateProperty<T> property, T value) where T : notnull
    {
        int propertyIndex = Definition.GetPropertyIndex(property);
        int valueIndex = property.IndexOf(value);
        if (valueIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Value is not valid for state property '{property.Name}'.");
        }

        return GetNeighbors()[propertyIndex][valueIndex];
    }

    public BlockState TrySetValue<T>(StateProperty<T> property, T value) where T : notnull
    {
        if (!Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            return this;
        }

        int valueIndex = property.IndexOf(value);
        if (valueIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Value is not valid for state property '{property.Name}'.");
        }

        return GetNeighbors()[propertyIndex][valueIndex];
    }

    public BlockState Cycle<T>(StateProperty<T> property) where T : notnull
    {
        int propertyIndex = Definition.GetPropertyIndex(property);
        int currentValueIndex = property.IndexOf((T)_values[propertyIndex]);
        int nextValueIndex = (currentValueIndex + 1) % property.ValueCount;
        return GetNeighbors()[propertyIndex][nextValueIndex];
    }

    internal object[] CopyValues() => (object[])_values.Clone();

    internal void InitializeNeighbors(BlockState[][] neighbors)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        if (_neighbors is not null)
        {
            throw new InvalidOperationException("Block-state neighbors can only be initialized once.");
        }

        _neighbors = neighbors;
    }

    private BlockState[][] GetNeighbors() =>
        _neighbors ?? throw new InvalidOperationException("Block-state neighbors have not been initialized.");

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
