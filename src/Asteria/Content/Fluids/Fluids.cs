using Asteria.Content.Registries;

namespace Asteria.Content.Fluids;

public static class Fluids
{
    public static EmptyFluid EMPTY { get; } = new();

    internal static void RegisterAll()
    {
        Registry.Register(BuiltInRegistries.Fluid, FluidIds.EMPTY, EMPTY);

        foreach (Fluid fluid in BuiltInRegistries.Fluid)
        {
            foreach (FluidState state in fluid.StateDefinition.PossibleStates)
                Fluid.FluidStateRegistry.Add(state);
        }
    }
}
