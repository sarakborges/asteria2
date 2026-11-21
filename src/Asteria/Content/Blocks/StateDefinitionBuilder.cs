using Asteria.Content.Blocks.StateProperties;

namespace Asteria.Content.Blocks;

public class StateDefinitionBuilder<TOwner, TState>
    where TOwner : notnull
    where TState : StateHolder<TOwner, TState>
{
    private readonly Func<StateDefinition<TOwner, TState>, object[], TState> _stateFactory;
    private readonly List<StateProperty> _properties = new();

    protected TOwner Owner { get; }
    protected IReadOnlyList<StateProperty> Properties => _properties;

    public StateDefinitionBuilder(
        TOwner owner,
        Func<StateDefinition<TOwner, TState>, object[], TState> stateFactory)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
        _stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
    }

    public StateDefinitionBuilder<TOwner, TState> Add<T>(StateProperty<T> property)
        where T : notnull, IComparable
    {
        ArgumentNullException.ThrowIfNull(property);
        if (!StateProperty.IsValidName(property.Name))
            throw new ArgumentException($"{Owner} has invalidly named property: {property.Name}");
        if (property.ValueCount <= 1)
            throw new ArgumentException($"{Owner} attempted use property {property.Name} with <= 1 possible values");

        for (int index = 0; index < property.ValueCount; index++)
        {
            string serializedValue = property.GetSerializedValue(property.GetValueObject(index));
            if (!StateProperty.IsValidName(serializedValue))
                throw new ArgumentException($"{Owner} has property: {property.Name} with invalidly named value: {serializedValue}");
        }

        if (_properties.Any(existing => string.Equals(existing.Name, property.Name, StringComparison.Ordinal)))
            throw new ArgumentException($"{Owner} has duplicate property: {property.Name}");

        _properties.Add(property);
        return this;
    }

    public virtual StateDefinition<TOwner, TState> Build() =>
        new(Owner, _properties, _stateFactory);
}

public sealed class StateDefinitionBuilder : StateDefinitionBuilder<Block, BlockState>
{
    public StateDefinitionBuilder(Block owner)
        : base(owner, static (definition, values) => new BlockState((StateDefinition)definition, values))
    {
    }

    public override StateDefinition Build() => new(Owner, Properties);

    internal StateDefinition Build(Block owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!ReferenceEquals(owner, Owner))
            throw new ArgumentException("State definition owner does not match the builder owner.", nameof(owner));
        return Build();
    }
}
