using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

public abstract class StateHolder<TOwner, TState>
    where TOwner : notnull
    where TState : StateHolder<TOwner, TState>
{
    private readonly object[] _values;
    private TState[][]? _neighbors;

    public TOwner Owner => Definition.Owner;
    public StateDefinition<TOwner, TState> Definition { get; }
    public bool IsSingletonState => _values.Length == 0;

    public sealed override bool Equals(object? obj) =>
        base.Equals(obj);

    public override int GetHashCode() =>
        base.GetHashCode();

    protected StateHolder(StateDefinition<TOwner, TState> definition, object[] values)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public T GetValue<T>(StateProperty<T> property) where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int index))
            throw new ArgumentException($"Cannot get property {property?.ToString() ?? "null"} as it does not exist in {Owner}");
        return (T)_values[index];
    }

    public bool TryGetValue<T>(StateProperty<T> property, out T value)
        where T : notnull, IComparable
    {
        if (Definition.TryGetPropertyIndex(property, out int index))
        {
            value = (T)_values[index];
            return true;
        }

        value = default!;
        return false;
    }

    public T GetValueOrElse<T>(StateProperty<T> property, T defaultValue)
        where T : notnull, IComparable =>
        TryGetValue(property, out T value) ? value : defaultValue;

    public IEnumerable<IStatePropertyValue> GetValues()
    {
        for (int index = 0; index < _values.Length; index++)
            yield return Definition.Properties[index].CreateValue(_values[index]);
    }

    public IReadOnlyList<StateProperty> GetProperties() => Definition.Properties;

    public bool HasProperty(StateProperty property) => Definition.HasProperty(property);

    public TState SetValue<T>(StateProperty<T> property, T value)
        where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int index))
            throw new ArgumentException($"Cannot set property {property?.ToString() ?? "null"} as it does not exist in {Owner}");
        return SetValueInternal(property, index, value);
    }

    public TState TrySetValue<T>(StateProperty<T> property, T value)
        where T : notnull, IComparable
    {
        if (!Definition.TryGetPropertyIndex(property, out int index))
            return (TState)this;
        return SetValueInternal(property, index, value);
    }

    public TState Cycle<T>(StateProperty<T> property)
        where T : notnull, IComparable
    {
        T currentValue = GetValue(property);
        int currentValueIndex = property.IndexOf(currentValue);
        int nextValueIndex = (currentValueIndex + 1) % property.ValueCount;
        return SetValue(property, property.PossibleValues[nextValueIndex]);
    }

    private TState SetValueInternal<T>(StateProperty<T> property, int propertyIndex, T value)
        where T : notnull, IComparable
    {
        int valueIndex = property.IndexOf(value);
        if (valueIndex < 0)
            throw new ArgumentException($"Cannot set property {property} to {value} on {Owner}, it is not an allowed value");
        return GetNeighbors()[propertyIndex][valueIndex];
    }

    internal object GetValueObject(StateProperty property)
    {
        if (!Definition.TryGetPropertyIndex(property, out int index))
            throw new ArgumentException(
                $"Cannot get property {property} as it does not exist in {Owner}");
        return _values[index];
    }

    internal object[] CopyValues() => (object[])_values.Clone();

    internal void InitializeNeighbors(TState[][] neighbors)
    {
        ArgumentNullException.ThrowIfNull(neighbors);
        if (_neighbors is not null)
            throw new InvalidOperationException(string.Empty);
        _neighbors = neighbors;
    }

    private TState[][] GetNeighbors() =>
        _neighbors ?? throw new InvalidOperationException("State neighbors have not been initialized.");

    public override string ToString()
    {
        string owner = Owner.ToString() ?? typeof(TOwner).Name;
        if (IsSingletonState)
            return owner;

        var properties = new string[_values.Length];
        for (int index = 0; index < _values.Length; index++)
        {
            StateProperty property = Definition.Properties[index];
            properties[index] = $"{property.Name}={property.GetSerializedValue(_values[index])}";
        }

        return $"{owner}[{string.Join(",", properties)}]";
    }
}
