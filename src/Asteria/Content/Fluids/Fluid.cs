using Asteria.Content.Blocks;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

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

    public abstract float GetHeight(FluidState state, BlockGetter level, BlockPos position);

    public abstract float GetOwnHeight(FluidState state);

    public abstract VoxelShape GetShape(FluidState state, BlockGetter level, BlockPos position);

    public bool Is(TagKey<Fluid> tag) =>
        BuiltInRegistryHolder?.Is(tag) ?? false;

    public virtual AABB? GetAABB(FluidState state, BlockGetter level, BlockPos position)
    {
        if (IsEmpty)
            return null;

        float height = state.GetHeight(level, position);
        return new AABB(
            position.GetX(), position.GetY(), position.GetZ(),
            position.GetX() + 1.0, (float)position.GetY() + height, position.GetZ() + 1.0);
    }

    public virtual bool IsSame(Fluid other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(this, other);
    }

    public override string ToString() =>
        BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ?? "[unregistered fluid]";
}
