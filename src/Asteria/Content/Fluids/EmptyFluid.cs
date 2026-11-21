namespace Asteria.Content.Fluids;

public sealed class EmptyFluid : Fluid
{
    public override bool IsEmpty => true;

    public override bool IsSource(FluidState state) => false;

    public override int GetAmount(FluidState state) => 0;

    public override float GetOwnHeight(FluidState state) => 0.0f;
}
