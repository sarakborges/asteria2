namespace Asteria.Content.Fluids;

public sealed class FluidState : StateHolder<Fluid, FluidState>
{
    public const int AmountMax = 9;
    public const int AmountFull = 8;

    public Fluid Fluid => Owner;

    internal FluidState(StateDefinition<Fluid, FluidState> definition, object[] values)
        : base(definition, values)
    {
    }

    public bool IsSource() => Fluid.IsSource(this);

    public bool IsSourceOfType(Fluid fluidType)
    {
        ArgumentNullException.ThrowIfNull(fluidType);
        return ReferenceEquals(Fluid, fluidType) && Fluid.IsSource(this);
    }

    public bool IsEmpty() => Fluid.IsEmpty;

    public bool IsRandomlyTicking() => Fluid.IsRandomlyTicking;

    public int GetAmount() => Fluid.GetAmount(this);

    public float GetOwnHeight() => Fluid.GetOwnHeight(this);

    public bool IsFull() => GetAmount() == AmountFull;
}
