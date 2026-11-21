using Asteria.Content.Registries;

namespace Asteria.Content.Fluids;

public abstract class Fluid
{
    public static FluidStateIdMap FluidStateRegistry { get; } = new();

    public StateDefinition<Fluid, FluidState> StateDefinition { get; }
    public FluidState DefaultFluidState { get; private set; } = null!;
    public Holder<Fluid>.Reference? BuiltInRegistryHolder { get; }

    protected Fluid(bool createIntrusiveRegistryHolder = true)
    {
        if (createIntrusiveRegistryHolder)
            BuiltInRegistryHolder = BuiltInRegistries.Fluid.CreateIntrusiveHolder(this);

        var builder = new StateDefinitionBuilder<Fluid, FluidState>(
            this,
            static (definition, values) => new FluidState(definition, values));
        CreateFluidStateDefinition(builder);
        StateDefinition = builder.Build();
        DefaultFluidState = StateDefinition.Any();
    }

    protected virtual void CreateFluidStateDefinition(
        StateDefinitionBuilder<Fluid, FluidState> builder)
    {
    }

    protected void RegisterDefaultState(FluidState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(state.Fluid, this))
            throw new ArgumentException("Default fluid state must belong to this fluid.", nameof(state));
        DefaultFluidState = state;
    }

    public virtual bool IsEmpty => false;

    public virtual bool IsRandomlyTicking => false;

    public abstract bool IsSource(FluidState state);

    public abstract int GetAmount(FluidState state);

    public abstract float GetOwnHeight(FluidState state);

    public virtual bool IsSame(Fluid other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(this, other);
    }

    public override string ToString() =>
        BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ?? "[unregistered fluid]";
}
