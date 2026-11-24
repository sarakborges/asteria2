using Asteria.Content.Registries;

namespace Asteria.Content.Fluids;

public static class Fluids
{
    public static EmptyFluid EMPTY { get; } = new();
    public static WaterFluid WATER { get; } = new();
    public static LavaFluid LAVA { get; } = new();

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Fluid, FluidIds.EMPTY, EMPTY);
        Registry.Register(BuiltInRegistries.Fluid, FluidIds.WATER, WATER);
        Registry.Register(BuiltInRegistries.Fluid, FluidIds.LAVA, LAVA);

        foreach (Fluid fluid in BuiltInRegistries.Fluid)
        {
            foreach (FluidState state in fluid.StateDefinition.PossibleStates)
                Fluid.FluidStateRegistry.Add(state);
        }
    }
}
