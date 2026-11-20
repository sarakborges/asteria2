using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

/// <summary>
/// StateHolder equivalent. Property transitions resolve through precomputed neighboring states.
/// </summary>
public sealed class BlockState
{
    private readonly object[] _values;
    private BlockState[][]? _neighbors;

    public Block Block => Definition.Owner;
    public StateDefinition Definition { get; }
    public bool IsSingletonState => _values.Length == 0;

    // BlockStateBase caches these values when each state is constructed.
    public bool IsAir { get; }
    public float DestroySpeed { get; }
    public bool RequiresCorrectToolForDrops { get; }
    public bool CanOcclude { get; }
    public bool IsRandomlyTicking { get; }
    public bool IgnitedByLava { get; }
    public bool IsLiquid { get; }
    public bool IsReplaceable { get; }
    public bool ShouldSpawnTerrainParticles { get; }

    internal BlockState(StateDefinition definition, object[] values)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _values = values ?? throw new ArgumentNullException(nameof(values));

        BlockProperties properties = Definition.Owner.Properties;
        IsAir = properties.IsAir;
        DestroySpeed = properties.DestroyTimeValue;
        RequiresCorrectToolForDrops = properties.RequiresCorrectToolForDropsValue;
        CanOcclude = properties.CanOccludeValue;
        IsRandomlyTicking = properties.IsRandomlyTickingValue;
        IgnitedByLava = properties.IgnitedByLavaValue;
        IsLiquid = properties.IsLiquidValue;
        IsReplaceable = properties.ReplaceableValue;
        ShouldSpawnTerrainParticles = properties.SpawnTerrainParticlesValue;

        global::Asteria.Content.Blocks.Block.BlockStateRegistry.Add(this);
    }

    public T GetValue<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            throw new ArgumentException(
                $"Cannot get property {property?.ToString() ?? "null"} as it does not exist in {Block}");
        }

        return (T)_values[propertyIndex];
    }

    public bool TryGetValue<T>(StateProperty<T> property, out T value) where T : notnull, IComparable
    {
        if (Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            value = (T)_values[propertyIndex];
            return true;
        }

        value = default!;
        return false;
    }

    public T GetValueOrElse<T>(StateProperty<T> property, T defaultValue) where T : notnull, IComparable =>
        TryGetValue(property, out T value) ? value : defaultValue;

    public IEnumerable<IStatePropertyValue> GetValues()
    {
        for (int index = 0; index < _values.Length; index++)
        {
            yield return Definition.Properties[index].CreateValue(_values[index]);
        }
    }

    public IReadOnlyList<StateProperty> GetProperties() => Definition.Properties;

    public bool HasProperty(StateProperty property) => Definition.HasProperty(property);

    public BlockState SetValue<T>(StateProperty<T> property, T value) where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            throw new ArgumentException(
                $"Cannot set property {property?.ToString() ?? "null"} as it does not exist in {Block}");
        }

        return SetValueInternal(property, propertyIndex, value);
    }

    public BlockState TrySetValue<T>(StateProperty<T> property, T value) where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int propertyIndex))
        {
            return this;
        }

        return SetValueInternal(property, propertyIndex, value);
    }

    public BlockState Cycle<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        T currentValue = GetValue(property);
        int currentValueIndex = property.IndexOf(currentValue);
        int nextValueIndex = (currentValueIndex + 1) % property.ValueCount;
        return SetValue(property, property.PossibleValues[nextValueIndex]);
    }

    private BlockState SetValueInternal<T>(
        StateProperty<T> property,
        int propertyIndex,
        T value) where T : notnull, IComparable
    {
        int valueIndex = property.IndexOf(value);
        if (valueIndex < 0)
        {
            throw new ArgumentException(
                $"Cannot set property {property} to {value} on {Block}, it is not an allowed value");
        }

        return GetNeighbors()[propertyIndex][valueIndex];
    }

    internal object[] CopyValues() => (object[])_values.Clone();

    internal void InitializeNeighbors(BlockState[][] neighbors)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        if (_neighbors is not null)
        {
            throw new InvalidOperationException(string.Empty);
        }

        _neighbors = neighbors;
    }

    private BlockState[][] GetNeighbors() =>
        _neighbors ?? throw new InvalidOperationException("Block-state neighbors have not been initialized.");

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
