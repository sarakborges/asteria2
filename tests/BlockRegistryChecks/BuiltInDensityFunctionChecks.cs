using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class BuiltInDensityFunctionChecks
{
    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("The initial density-function entries must not require noise samplers.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("The initial density-function entries must not require random sources.");

        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("The initial density-function entries must not require End-island random sources.");
    }

    public static void Run()
    {
        CheckRegistryAndRegistrationOrder();
        CheckZeroFunction();
        CheckYGradient();
    }

    private static void CheckRegistryAndRegistrationOrder()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;
        string[] actualPaths = registry.Entries
            .Select(holder => holder.Key.Location.Path)
            .ToArray();

        Require(
            registry.Location == ResourceLocation.Parse("asteria:worldgen/density_function") &&
            registry.Key.RegistryLocation == BuiltInRegistries.RootLocation,
            "The density-function value registry must use Asteria's worldgen/density_function registry key.");

        Require(
            actualPaths.SequenceEqual(new[] { "zero", "y" }, StringComparer.Ordinal),
            "The initial density-function registry entries must preserve the first two NoiseRouterData.bootstrap registrations: zero, then y.");

        Require(
            registry.Entries.All(holder => holder.IsBound),
            "Every bootstrapped density-function value must have a bound registry holder.");
    }

    private static void CheckZeroFunction()
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ZERO) ??
            throw new InvalidOperationException("Missing built-in zero density-function holder.");

        Require(
            holder.IsBound &&
            ReferenceEquals(holder.Value, DensityFunctions.Zero()) &&
            holder.Value is ConstantFunction { Value: 0.0f },
            "The zero density-function entry must register the canonical zero singleton.");
    }

    private static void CheckYGradient()
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.Y) ??
            throw new InvalidOperationException("Missing built-in y density-function holder.");

        Require(holder.IsBound, "The y density-function holder must be bound.");

        if (holder.Value is not GradientFunction gradient)
            throw new InvalidOperationException("The y density-function entry must use a gradient node.");

        int belowBottom = NoiseSettings.DimensionMinY * 2;
        int aboveTop = NoiseSettings.DimensionMaxY * 2;

        Require(
            ReferenceEquals(gradient.Axis, Direction.Axis.Y) &&
            gradient.Tiling == TilingMode.CLAMP_TO_EDGE &&
            gradient.FromCoordinate == belowBottom &&
            gradient.ToCoordinate == aboveTop &&
            gradient.FromValue == belowBottom &&
            gradient.ToValue == aboveTop,
            "The y gradient must use the exact dimension-derived vertical endpoints and clamped tiling.");

        DensitySampler sampler = gradient.CompileSampler(new UnusedCompileContext());
        SamplerContext context = SamplerContext.EmptyUncached;

        Require(
            sampler.SampleValue(context, 0, belowBottom, 0) == belowBottom &&
            sampler.SampleValue(context, 0, 0, 0) == 0.0f &&
            sampler.SampleValue(context, 0, aboveTop, 0) == aboveTop,
            "The y gradient must map the dimension's bottom, origin and top to their matching density values.");

        Require(
            sampler.SampleValue(context, 0, belowBottom - 1, 0) == belowBottom &&
            sampler.SampleValue(context, 0, aboveTop + 1, 0) == aboveTop,
            "The y gradient must clamp samples outside the dimension-derived vertical bounds.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
