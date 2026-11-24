using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.Util.Context;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;
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

    private sealed class TestChunkBlender(bool isEmpty) : IChunkBlender
    {
        public bool IsEmpty { get; } = isEmpty;

        public float BlendDensity(int blockX, int blockY, int blockZ, float density) =>
            density + blockX + blockY * 10.0f + blockZ * 100.0f;
    }

    public static void Run()
    {
        CheckRegistryAndRegistrationOrder();
        CheckZeroFunction();
        CheckYGradient();
        CheckOreVeinFunctions();
        CheckShiftFunctions();
        CheckBase3dNoiseFunctions();
        CheckOverworldClimateFunctions();
        CheckOverworldTerrainDensityHelpers();
        CheckSimpleDensityFunctions();
        CheckBlendDensityFunction();
        CheckOverworldTerrainRegistration();
        CheckMappedNoiseFactories();
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

        string[] expectedPaths =
        [
            "zero", "y",
            "overworld/ore_vein/toggle",
            "overworld/ore_vein/richness",
            "overworld/ore_vein/mask",
            "overworld/ore_vein/copper_density",
            "overworld/ore_vein/iron_density",
            "overworld/ore_vein/gap",
            "shift_x", "shift_z",
            "overworld/base_3d_noise", "nether/base_3d_noise", "end/base_3d_noise",
            "overworld/temperature", "overworld_large_biomes/temperature",
            "overworld/vegetation", "overworld_large_biomes/vegetation",
            "overworld/continents", "overworld/erosion", "overworld/ridges",
            "overworld/ridges_folded",
            "overworld_large_biomes/continents", "overworld_large_biomes/erosion",
            "overworld/offset", "overworld/factor", "overworld/depth", "overworld/jaggedness",
            "overworld/sloped_cheese", "overworld/preliminary_surface_level", "overworld/chunk_surface_level",
            "overworld_large_biomes/offset", "overworld_large_biomes/factor",
            "overworld_large_biomes/depth", "overworld_large_biomes/jaggedness",
            "overworld_large_biomes/sloped_cheese", "overworld_large_biomes/preliminary_surface_level",
            "overworld_large_biomes/chunk_surface_level",
            "overworld_amplified/offset", "overworld_amplified/factor",
            "overworld_amplified/depth", "overworld_amplified/jaggedness",
            "overworld_amplified/sloped_cheese", "overworld_amplified/preliminary_surface_level",
            "overworld_amplified/chunk_surface_level"
        ];

        Require(
            actualPaths.SequenceEqual(expectedPaths, StringComparer.Ordinal),
            $"The density-function registry must preserve the Minecraft 26.3 NoiseRouterData.bootstrap order. Expected [{string.Join(", ", expectedPaths)}], got [{string.Join(", ", actualPaths)}].");

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


    private static void CheckMappedNoiseFactories()
    {
        Holder<NormalNoise> noiseHolder =
            BuiltInRegistries.Noise.GetHolder(Noises.SPAGHETTI_3D_THICKNESS) ??
            throw new InvalidOperationException("Missing built-in spaghetti 3D thickness noise holder.");

        DensityFunction mapped = DensityFunctions.MappedNoise(
            noiseHolder,
            2.0,
            0.5,
            -0.065f,
            -0.088f);

        Require(
            mapped is BinaryFunction
            {
                Operation: BinaryFunction.Type.ADD,
                Left: BinaryFunction
                {
                    Operation: BinaryFunction.Type.MUL,
                    Left: NoiseFunction
                    {
                        XzScale: 2.0,
                        YScale: 0.5,
                        NoiseData: var actualNoise,
                        ShiftX: ConstantFunction { Value: 0.0f },
                        ShiftY: ConstantFunction { Value: 0.0f },
                        ShiftZ: ConstantFunction { Value: 0.0f }
                    },
                    Right: ConstantFunction { Value: var scale }
                },
                Right: ConstantFunction { Value: var offset }
            } &&
            ReferenceEquals(actualNoise, noiseHolder) &&
            MathF.Abs(scale - (-0.0115f)) < 0.000001f &&
            MathF.Abs(offset - (-0.0765f)) < 0.000001f,
            "mapped_noise must build the vanilla unshifted noise node and exact linear remap from [-1, 1] to its target interval.");

        DensityFunction yScaled = DensityFunctions.MappedNoise(
            noiseHolder,
            0.75,
            -0.1f,
            0.2f);

        Require(
            yScaled is BinaryFunction
            {
                Operation: BinaryFunction.Type.ADD,
                Left: BinaryFunction
                {
                    Operation: BinaryFunction.Type.MUL,
                    Left: NoiseFunction { XzScale: 1.0, YScale: 0.75 }
                }
            },
            "The y-scale mapped_noise overload must default its horizontal scale to one.");

        DensityFunction defaultScale = DensityFunctions.MappedNoise(
            noiseHolder,
            -1.0f,
            1.0f);

        Require(
            defaultScale is BinaryFunction
            {
                Operation: BinaryFunction.Type.MUL,
                Left: NoiseFunction { XzScale: 1.0, YScale: 1.0 },
                Right: ConstantFunction { Value: 1.0f }
            },
            "The two-target mapped_noise overload must default both scales to one and omit a zero remap offset.");

        bool nullRejected = false;
        try
        {
            DensityFunctions.MappedNoise(null!, 1.0, 1.0, -1.0f, 1.0f);
        }
        catch (ArgumentNullException)
        {
            nullRejected = true;
        }

        Require(
            nullRejected,
            "mapped_noise must reject a null normal-noise holder.");
    }

    private static void CheckOverworldTerrainRegistration()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;

        CheckTerrainProfile(
            registry,
            DensityFunctionKeys.OVERWORLD_FUNCTIONS,
            amplified: false,
            keyPrefix: "normal");
        CheckTerrainProfile(
            registry,
            DensityFunctionKeys.LARGE_OVERWORLD_FUNCTIONS,
            amplified: false,
            keyPrefix: "large-biomes");
        CheckTerrainProfile(
            registry,
            DensityFunctionKeys.AMPLIFIED_OVERWORLD_FUNCTIONS,
            amplified: true,
            keyPrefix: "amplified");
    }

    private static void CheckTerrainProfile(
        Registry<DensityFunction> registry,
        OverworldFunctionSet<RegistryKey<DensityFunction>> names,
        bool amplified,
        string keyPrefix)
    {
        DensityFunction offset = registry.GetValueOrThrow(names.Offset);
        DensityFunction factor = registry.GetValueOrThrow(names.Factor);
        DensityFunction depth = registry.GetValueOrThrow(names.Depth);
        DensityFunction jaggedness = registry.GetValueOrThrow(names.Jaggedness);
        DensityFunction slopedCheese = registry.GetValueOrThrow(names.SlopedCheese);
        DensityFunction preliminarySurfaceLevel =
            registry.GetValueOrThrow(names.PreliminarySurfaceLevel);
        DensityFunction chunkSurfaceLevel =
            registry.GetValueOrThrow(names.ChunkSurfaceLevel);

        Require(
            offset is CacheFunction
            {
                Input: LerpFunction
                {
                    Alpha: var offsetBlendAlpha,
                    First: var offsetBlendTarget,
                    Second: BinaryFunction
                    {
                        Operation: BinaryFunction.Type.ADD,
                        Left: ConstantFunction { Value: -0.50375f },
                        Right: SplineFunction
                    }
                }
            } &&
            ReferenceEquals(offsetBlendAlpha, SimpleDensityFunction.BLEND_ALPHA) &&
            ReferenceEquals(offsetBlendTarget, SimpleDensityFunction.BLEND_OFFSET),
            $"{keyPrefix} offset must use the vanilla global offset and blend_offset spline wrapper.");

        Require(
            factor is CacheFunction
            {
                Input: LerpFunction
                {
                    Alpha: var factorBlendAlpha,
                    First: ConstantFunction { Value: 10.0f },
                    Second: SplineFunction
                }
            } &&
            ReferenceEquals(factorBlendAlpha, SimpleDensityFunction.BLEND_ALPHA),
            $"{keyPrefix} factor must blend its vanilla spline toward the constant factor 10.");

        Require(
            depth is BinaryFunction
            {
                Operation: BinaryFunction.Type.ADD,
                Left: GradientFunction
                {
                    Axis: var depthAxis,
                    Tiling: TilingMode.CLAMP_TO_EDGE,
                    FromCoordinate: -64,
                    ToCoordinate: 320,
                    FromValue: 1.5f,
                    ToValue: -1.5f
                },
                Right: DensityFunctions.HolderHolder { Function: var offsetHolder }
            } &&
            ReferenceEquals(depthAxis, Direction.Axis.Y) &&
            ReferenceEquals(offsetHolder, registry.GetHolder(names.Offset)),
            $"{keyPrefix} depth must be offset_to_depth of the registered offset holder.");

        Require(
            jaggedness is CacheFunction
            {
                Input: LerpFunction
                {
                    Alpha: var jaggednessBlendAlpha,
                    First: ConstantFunction { Value: 0.0f },
                    Second: SplineFunction
                }
            } &&
            ReferenceEquals(jaggednessBlendAlpha, SimpleDensityFunction.BLEND_ALPHA),
            $"{keyPrefix} jaggedness must be the unscaled spline blended toward zero and cached.");

        Require(
            slopedCheese is CacheFunction
            {
                Input: BinaryFunction
                {
                    Operation: BinaryFunction.Type.ADD,
                    Right: DensityFunctions.HolderHolder { Function: var baseNoise }
                }
            } &&
            ReferenceEquals(
                baseNoise,
                registry.GetHolder(DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD)),
            $"{keyPrefix} sloped_cheese must cache the initial density plus the shared Overworld base 3D noise.");

        Require(
            preliminarySurfaceLevel is FindTopSurfaceFunction
            {
                LowerBound: -64,
                CellHeight: 8
            },
            $"{keyPrefix} preliminary_surface_level must use find_top_surface with the vanilla vertical sampling bounds.");

        Require(
            chunkSurfaceLevel is InterpolatedFunction
            {
                CellSizeXz: 16,
                CellSizeY: 1,
                Input: DensityFunctions.HolderHolder { Function: var surfaceLevel }
            } &&
            ReferenceEquals(surfaceLevel, registry.GetHolder(names.PreliminarySurfaceLevel)),
            $"{keyPrefix} chunk_surface_level must interpolate the registered preliminary surface level at 16x1 cells.");

        if (amplified)
        {
            Require(
                !names.Offset.Equals(DensityFunctionKeys.OVERWORLD_FUNCTIONS.Offset) &&
                !names.Factor.Equals(DensityFunctionKeys.OVERWORLD_FUNCTIONS.Factor) &&
                !names.Jaggedness.Equals(DensityFunctionKeys.OVERWORLD_FUNCTIONS.Jaggedness),
                "Amplified terrain must register distinct offset, factor and jaggedness entries.");
        }
    }

    private static void CheckBlendDensityFunction()
    {
        DensityFunction child = new ConstantFunction(0.5f);
        DensityFunction function = DensityFunctions.BlendDensity(child);

        Require(
            function is BlendDensityFunction { Input: var input } &&
            ReferenceEquals(input, child) &&
            function.Range().Equals(child.Range()) &&
            function.DomainAxes() == child.DomainAxes(),
            "blend_density must preserve its child, range and coordinate-domain contract.");

        var sampler = function.CompileSampler(new UnusedCompileContext());
        Require(
            sampler.SampleValue(SamplerContext.EmptyUncached, 1, 2, 3) == 0.5f,
            "blend_density must pass through the child when no chunk blender is present.");

        var emptyBlenderContext = SamplerContext.CreateBuilder()
            .SetUserFields(ContextMap.CreateBuilder()
                .WithParameter(DensityFunctionContextKeys.BLENDER, new TestChunkBlender(isEmpty: true))
                .Build())
            .Build();

        Require(
            sampler.SampleValue(emptyBlenderContext, 1, 2, 3) == 0.5f,
            "blend_density must pass through the child when the chunk blender is empty.");

        var activeBlender = new TestChunkBlender(isEmpty: false);
        var activeContext = SamplerContext.CreateBuilder()
            .SetUserFields(ContextMap.CreateBuilder()
                .WithParameter(DensityFunctionContextKeys.BLENDER, activeBlender)
                .Build())
            .Build();

        Require(
            sampler.SampleValue(activeContext, 1, 2, 3) == 321.5f,
            "blend_density must delegate the sampled value and exact block coordinates to the active chunk blender.");

        var volume = new DensityVolume(
            2, 2, 2,
            minBlockX: 1,
            minBlockY: 2,
            minBlockZ: 3,
            stepBlockX: 2,
            stepBlockY: 4,
            stepBlockZ: 3);
        using ScopedDensityBuffer buffer = activeContext.AcquireBuffer(volume);
        sampler.SampleVolume(activeContext, buffer, volume);

        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            int blockX = volume.BlockX(x);
            int blockY = volume.BlockY(y);
            int blockZ = volume.BlockZ(z);
            float expected = 0.5f + blockX + blockY * 10.0f + blockZ * 100.0f;
            Require(
                buffer.Get(volume.IndexUnchecked(x, y, z)) == expected,
                $"blend_density volume sampling must preserve stepped block coordinates at {blockX},{blockY},{blockZ}.");
        }

        DensityFunction replacement = new ConstantFunction(0.75f);
        DensityFunction rewritten = function.RewriteChildren(
            candidate => ReferenceEquals(candidate, child) ? replacement : candidate);
        Require(
            rewritten is BlendDensityFunction { Input: var rewrittenInput } &&
            ReferenceEquals(rewrittenInput, replacement),
            "blend_density must rewrite its input child without dropping the wrapper.");
    }

    private static void CheckSimpleDensityFunctions()
    {
        DensityFunction[] functions =
        [
            DensityFunctions.BlendAlpha(),
            DensityFunctions.BlendOffset(),
            DensityFunctions.Beardifier()
        ];

        Require(
            ReferenceEquals(functions[0], SimpleDensityFunction.BLEND_ALPHA) &&
            ReferenceEquals(functions[1], SimpleDensityFunction.BLEND_OFFSET) &&
            ReferenceEquals(functions[2], SimpleDensityFunction.BEARDIFIER),
            "The simple density-function factories must return the three canonical vanilla singleton nodes.");

        Require(
            SimpleDensityFunction.BLEND_ALPHA.Range().Equals(Interval.Of(0.0f, 1.0f)) &&
            SimpleDensityFunction.BLEND_OFFSET.Range().Equals(Interval.INFINITE) &&
            SimpleDensityFunction.BEARDIFIER.Range().Equals(Interval.INFINITE),
            "Simple density-function ranges must match vanilla: blend alpha is [0, 1], blend offset and beardifier are unbounded.");

        Require(
            SimpleDensityFunction.BLEND_ALPHA.DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            SimpleDensityFunction.BLEND_OFFSET.DomainAxes() ==
                (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            SimpleDensityFunction.BEARDIFIER.DomainAxes() == DensityFunction.ALL_AXES,
            "Simple density-function domains must preserve their vanilla XZ and XYZ axis masks.");

        var compileContext = new UnusedCompileContext();
        var alpha = (ContextBoundSampler)SimpleDensityFunction.BLEND_ALPHA.CompileSampler(compileContext);
        var offset = (ContextBoundSampler)SimpleDensityFunction.BLEND_OFFSET.CompileSampler(compileContext);
        var beardifier = (ContextBoundSampler)SimpleDensityFunction.BEARDIFIER.CompileSampler(compileContext);

        Require(
            ReferenceEquals(alpha.Key, DensityFunctionContextKeys.BLEND_ALPHA) &&
            ReferenceEquals(offset.Key, DensityFunctionContextKeys.BLEND_OFFSET) &&
            ReferenceEquals(beardifier.Key, DensityFunctionContextKeys.BEARDIFIER) &&
            alpha.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 1.0f &&
            offset.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 0.0f &&
            beardifier.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 0.0f,
            "Simple density samplers must use their exact context keys and vanilla fallback values.");

        ContextMap fields = ContextMap.CreateBuilder()
            .WithParameter(
                DensityFunctionContextKeys.BLEND_ALPHA,
                new ConstantFunction.Sampler(0.25f))
            .WithParameter(
                DensityFunctionContextKeys.BLEND_OFFSET,
                new ConstantFunction.Sampler(-3.5f))
            .WithParameter(
                DensityFunctionContextKeys.BEARDIFIER,
                new ConstantFunction.Sampler(2.75f))
            .Build();
        SamplerContext context = SamplerContext.CreateBuilder()
            .SetUserFields(fields)
            .Build();

        Require(
            alpha.SampleValue(context, 12, 48, -7) == 0.25f &&
            offset.SampleValue(context, 12, 48, -7) == -3.5f &&
            beardifier.SampleValue(context, 12, 48, -7) == 2.75f,
            "Simple density samplers must defer sampling to the sampler-context override when one is supplied.");

        Require(
            ReferenceEquals(
                SimpleDensityFunction.BLEND_ALPHA.RewriteChildren(static function => function),
                SimpleDensityFunction.BLEND_ALPHA) &&
            ReferenceEquals(
                SimpleDensityFunction.BLEND_OFFSET.RewriteChildren(static function => function),
                SimpleDensityFunction.BLEND_OFFSET) &&
            ReferenceEquals(
                SimpleDensityFunction.BEARDIFIER.RewriteChildren(static function => function),
                SimpleDensityFunction.BEARDIFIER),
            "Simple density functions have no children and must retain their canonical singleton instances after rewrites.");
    }

    private static void CheckOverworldTerrainDensityHelpers()
    {
        DensityFunction offset = DensityFunctions.Constant(0.25f);
        DensityFunction depth = BuiltInDensityFunctions.OffsetToDepth(offset);

        Require(
            depth is BinaryFunction
            {
                Operation: BinaryFunction.Type.ADD,
                Left: GradientFunction
                {
                    Axis: var axis,
                    Tiling: TilingMode.CLAMP_TO_EDGE,
                    FromCoordinate: -64,
                    ToCoordinate: 320,
                    FromValue: 1.5f,
                    ToValue: -1.5f
                },
                Right: ConstantFunction { Value: 0.25f }
            } && ReferenceEquals(axis, Direction.Axis.Y),
            "offset_to_depth must add the vanilla -64..320 Y gradient to the supplied offset.");

        DensityFunction factor = DensityFunctions.Constant(2.0f);
        DensityFunction depthWithJaggedness = DensityFunctions.Constant(3.0f);
        DensityFunction gradient =
            BuiltInDensityFunctions.NoiseGradientDensity(factor, depthWithJaggedness);

        Require(
            gradient is BinaryFunction
            {
                Operation: BinaryFunction.Type.MUL,
                Right: ConstantFunction { Value: 4.0f },
                Left: UnaryFunction
                {
                    Operation: UnaryFunction.Type.QUARTER_NEGATIVE,
                    Input: BinaryFunction
                    {
                        Operation: BinaryFunction.Type.MUL,
                        Left: ConstantFunction { Value: 3.0f },
                        Right: ConstantFunction { Value: 2.0f }
                    }
                }
            },
            "noise_gradient_density must multiply depth by factor, apply quarter_negative, then multiply by four.");

        bool offsetNullRejected = false;
        try
        {
            BuiltInDensityFunctions.OffsetToDepth(null!);
        }
        catch (ArgumentNullException)
        {
            offsetNullRejected = true;
        }

        bool factorNullRejected = false;
        try
        {
            BuiltInDensityFunctions.NoiseGradientDensity(null!, depthWithJaggedness);
        }
        catch (ArgumentNullException)
        {
            factorNullRejected = true;
        }

        bool depthNullRejected = false;
        try
        {
            BuiltInDensityFunctions.NoiseGradientDensity(factor, null!);
        }
        catch (ArgumentNullException)
        {
            depthNullRejected = true;
        }

        Require(
            offsetNullRejected && factorNullRejected && depthNullRejected,
            "Overworld terrain density helpers must reject null density-function inputs.");

        CheckPreliminarySurfaceLevel(amplified: false, expectedTopStart: 240, expectedTopEnd: 256, expectedBottomTarget: 0.1171875f);
        CheckPreliminarySurfaceLevel(amplified: true, expectedTopStart: 304, expectedTopEnd: 320, expectedBottomTarget: 0.4f);
    }

    private static void CheckPreliminarySurfaceLevel(
        bool amplified,
        int expectedTopStart,
        int expectedTopEnd,
        float expectedBottomTarget)
    {
        DensityFunction offset = DensityFunctions.Constant(0.125f);
        DensityFunction factor = DensityFunctions.Constant(2.0f);

        DensityFunction function = BuiltInDensityFunctions.PreliminarySurfaceLevel(
            offset,
            factor,
            amplified);

        Require(
            function is FindTopSurfaceFunction
            {
                LowerBound: -64,
                CellHeight: 8,
                UpperBound: ClampFunction { Min: -40.0f, Max: 320.0f }
            },
            "preliminary_surface_level must use find_top_surface with lower bound -64, cell height 8, and the clamped upper-bound function.");

        if (function is not FindTopSurfaceFunction surface ||
            surface.Density is not BinaryFunction
            {
                Operation: BinaryFunction.Type.ADD,
                Right: ConstantFunction { Value: -0.390625f },
                Left: LerpFunction
                {
                    Alpha: GradientFunction
                    {
                        Axis: var bottomAxis,
                        Tiling: TilingMode.CLAMP_TO_EDGE,
                        FromCoordinate: -64,
                        ToCoordinate: -40,
                        FromValue: 0.0f,
                        ToValue: 1.0f
                    },
                    First: ConstantFunction { Value: var bottomTarget },
                    Second: LerpFunction
                    {
                        Alpha: GradientFunction
                        {
                            Axis: var topAxis,
                            Tiling: TilingMode.CLAMP_TO_EDGE,
                            FromCoordinate: var topStart,
                            ToCoordinate: var topEnd,
                            FromValue: 1.0f,
                            ToValue: 0.0f
                        },
                        First: ConstantFunction { Value: -0.078125f },
                        Second: ClampFunction
                        {
                            Min: -64.0f,
                            Max: 64.0f,
                            Input: BinaryFunction
                            {
                                Operation: BinaryFunction.Type.ADD,
                                Right: ConstantFunction { Value: -0.703125f },
                                Left: BinaryFunction
                                {
                                    Operation: BinaryFunction.Type.MUL,
                                    Right: ConstantFunction { Value: 4.0f },
                                    Left: UnaryFunction
                                    {
                                        Operation: UnaryFunction.Type.QUARTER_NEGATIVE,
                                        Input: BinaryFunction
                                        {
                                            Operation: BinaryFunction.Type.MUL,
                                            Left: BinaryFunction { Operation: BinaryFunction.Type.ADD },
                                            Right: ConstantFunction { Value: 2.0f }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } ||
            !ReferenceEquals(bottomAxis, Direction.Axis.Y) ||
            !ReferenceEquals(topAxis, Direction.Axis.Y) ||
            topStart != expectedTopStart ||
            topEnd != expectedTopEnd ||
            bottomTarget != expectedBottomTarget)
        {
            throw new InvalidOperationException(
                $"The {(amplified ? "amplified" : "normal")} preliminary surface-level function must preserve vanilla slide order, Y ranges and targets.");
        }

        Require(
            surface.UpperBound is ClampFunction
            {
                Input: BinaryFunction
                {
                    Operation: BinaryFunction.Type.ADD,
                    Left: BinaryFunction
                    {
                        Operation: BinaryFunction.Type.MUL,
                        Right: ConstantFunction { Value: -128.0f },
                        Left: BinaryFunction
                        {
                            Operation: BinaryFunction.Type.SUB,
                            Left: BinaryFunction
                            {
                                Operation: BinaryFunction.Type.DIV,
                                Left: ConstantFunction { Value: 0.2734375f },
                                Right: ConstantFunction { Value: 2.0f }
                            },
                            Right: ConstantFunction { Value: 0.125f }
                        }
                    },
                    Right: ConstantFunction { Value: 128.0f }
                }
            },
            "preliminary_surface_level upper bound must preserve the vanilla remap expression and constants.");
    }

    private static void CheckShiftFunctions()
    {
        Holder<NormalNoise> noiseHolder =
            BuiltInRegistries.Noise.GetHolder(Noises.SHIFT) ??
            throw new InvalidOperationException("Missing built-in offset noise holder.");

        Holder<DensityFunction>.Reference shiftXHolder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_X) ??
            throw new InvalidOperationException("Missing built-in shift_x density-function holder.");
        Holder<DensityFunction>.Reference shiftZHolder =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_Z) ??
            throw new InvalidOperationException("Missing built-in shift_z density-function holder.");

        Require(
            shiftXHolder.Value is CacheFunction
            {
                Input: ShiftNoiseFunction.ShiftA shiftX
            } &&
            ReferenceEquals(shiftX.OffsetNoise, noiseHolder),
            "The shift_x density function must use the vanilla ShiftA node and the registered offset noise holder.");

        Require(
            shiftZHolder.Value is CacheFunction
            {
                Input: ShiftNoiseFunction.ShiftB shiftZ
            } &&
            ReferenceEquals(shiftZ.OffsetNoise, noiseHolder),
            "The shift_z density function must use the vanilla ShiftB node and the registered offset noise holder.");

        Require(
            shiftXHolder.Value.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shiftZHolder.Value.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            shiftXHolder.Value.Range().Equals(shiftZHolder.Value.Range()),
            "The registered horizontal shift functions must preserve their shared X/Z domain and range contract.");
    }

    private static void CheckOreVeinFunctions()
    {
        Holder<DensityFunction>.Reference toggle =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_TOGGLE) ??
            throw new InvalidOperationException("Missing ore-vein toggle density function.");
        Holder<DensityFunction>.Reference richness =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_RICHNESS) ??
            throw new InvalidOperationException("Missing ore-vein richness density function.");
        Holder<DensityFunction>.Reference mask =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_MASK) ??
            throw new InvalidOperationException("Missing ore-vein mask density function.");
        Holder<DensityFunction>.Reference copper =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_COPPER_DENSITY) ??
            throw new InvalidOperationException("Missing copper-vein density function.");
        Holder<DensityFunction>.Reference iron =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_IRON_DENSITY) ??
            throw new InvalidOperationException("Missing iron-vein density function.");
        Holder<DensityFunction>.Reference gap =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.ORE_VEIN_GAP) ??
            throw new InvalidOperationException("Missing ore-vein gap density function.");

        Require(
            toggle.Value is CacheFunction &&
            mask.Value is CacheFunction &&
            richness.Value is BinaryFunction { Operation: BinaryFunction.Type.ADD } &&
            copper.Value is RangeChoiceFunction &&
            iron.Value is RangeChoiceFunction &&
            gap.Value is BinaryFunction { Operation: BinaryFunction.Type.SUB },
            "Ore-vein density functions must use the vanilla cache, interpolation, range-choice and subtraction nodes.");

        Require(
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEININESS) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEIN_A) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_VEIN_B) is not null &&
            BuiltInRegistries.Noise.GetHolder(Noises.ORE_GAP) is not null,
            "Ore-vein density functions must reference the registered vanilla ore noise parameters.");

        Interval toggleRange = toggle.Value.Range();
        Interval richnessRange = richness.Value.Range();
        Interval copperRange = copper.Value.Range();
        Interval ironRange = iron.Value.Range();

        Require(
            toggleRange.Min() <= 0.0f &&
            toggleRange.Max() >= 0.0f &&
            MathF.Abs(richnessRange.Min() - 0.1f) < 0.0001f &&
            MathF.Abs(richnessRange.Max() - 0.3f) < 0.0001f &&
            copperRange.Min() <= -0.9999f &&
            copperRange.Max() >= 0.6999f &&
            ironRange.Min() <= -0.9999f &&
            ironRange.Max() >= 0.6999f,
            $"Ore-vein ranges must preserve vanilla bounds. toggle={toggleRange}, richness={richnessRange}, copper={copperRange}, iron={ironRange}.");
    }

    private static void CheckOverworldClimateFunctions()
    {
        Holder<DensityFunction>.Reference shiftX =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_X) ??
            throw new InvalidOperationException("Missing shift_x density function.");
        Holder<DensityFunction>.Reference shiftZ =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.SHIFT_Z) ??
            throw new InvalidOperationException("Missing shift_z density function.");

        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_TEMPERATURE,
            Noises.TEMPERATURE,
            shiftX,
            shiftZ,
            cached: false);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_LARGE_TEMPERATURE,
            Noises.TEMPERATURE_LARGE,
            shiftX,
            shiftZ,
            cached: false);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_VEGETATION,
            Noises.VEGETATION,
            shiftX,
            shiftZ,
            cached: false);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_LARGE_VEGETATION,
            Noises.VEGETATION_LARGE,
            shiftX,
            shiftZ,
            cached: false);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_CONTINENTS,
            Noises.CONTINENTALNESS,
            shiftX,
            shiftZ,
            cached: true);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_EROSION,
            Noises.EROSION,
            shiftX,
            shiftZ,
            cached: true);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.RIDGES,
            Noises.RIDGE,
            shiftX,
            shiftZ,
            cached: true);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_LARGE_CONTINENTS,
            Noises.CONTINENTALNESS_LARGE,
            shiftX,
            shiftZ,
            cached: true);
        CheckShiftedClimateNoise(
            DensityFunctionKeys.OVERWORLD_LARGE_EROSION,
            Noises.EROSION_LARGE,
            shiftX,
            shiftZ,
            cached: true);

        Holder<DensityFunction>.Reference ridgesFolded =
            BuiltInRegistries.DensityFunction.GetHolder(DensityFunctionKeys.RIDGES_FOLDED) ??
            throw new InvalidOperationException("Missing ridges_folded density function.");

        Require(
            ridgesFolded.Value is BinaryFunction
            {
                Operation: BinaryFunction.Type.MUL,
                Right: ConstantFunction { Value: -3.0f },
                Left: BinaryFunction
                {
                    Operation: BinaryFunction.Type.ADD,
                    Right: ConstantFunction { Value: -0.33333334f },
                    Left: UnaryFunction
                    {
                        Operation: UnaryFunction.Type.ABS,
                        Input: BinaryFunction
                        {
                            Operation: BinaryFunction.Type.ADD,
                            Right: ConstantFunction { Value: -0.6666667f },
                            Left: UnaryFunction { Operation: UnaryFunction.Type.ABS }
                        }
                    }
                }
            },
            "ridges_folded must preserve the vanilla peaks-and-valleys expression tree.");
    }

    private static void CheckShiftedClimateNoise(
        RegistryKey<DensityFunction> key,
        RegistryKey<NormalNoise> noiseKey,
        Holder<DensityFunction>.Reference shiftX,
        Holder<DensityFunction>.Reference shiftZ,
        bool cached)
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(key) ??
            throw new InvalidOperationException($"Missing climate density function: {key.Location}");

        DensityFunction node = cached
            ? holder.Value is CacheFunction cache
                ? cache.Input
                : throw new InvalidOperationException($"{key.Location} must be wrapped in cache.")
            : holder.Value;

        Holder<NormalNoise> noiseHolder =
            BuiltInRegistries.Noise.GetHolder(noiseKey) ??
            throw new InvalidOperationException($"Missing climate noise parameters: {noiseKey.Location}");

        Require(
            node is NoiseFunction
            {
                XzScale: 0.25,
                YScale: 0.0,
                ShiftX: DensityFunctions.HolderHolder { Function: var actualShiftX },
                ShiftY: ConstantFunction { Value: 0.0f },
                ShiftZ: DensityFunctions.HolderHolder { Function: var actualShiftZ }
            } noise &&
            ReferenceEquals(noise.NoiseData, noiseHolder) &&
            ReferenceEquals(actualShiftX, shiftX) &&
            ReferenceEquals(actualShiftZ, shiftZ),
            $"The {key.Location} density function must use its vanilla noise holder, 0.25 horizontal scale, zero Y scale and registered shift holders.");
    }

    private static void CheckBase3dNoiseFunctions()
    {
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_OVERWORLD,
            0.25, 0.125, 80.0, 160.0, 8.0,
            "overworld/base_3d_noise");
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_NETHER,
            0.25, 0.375, 80.0, 60.0, 8.0,
            "nether/base_3d_noise");
        CheckBase3dNoise(
            DensityFunctionKeys.BASE_3D_NOISE_END,
            0.25, 0.25, 80.0, 160.0, 4.0,
            "end/base_3d_noise");
    }

    private static void CheckBase3dNoise(
        RegistryKey<DensityFunction> key,
        double xzScale,
        double yScale,
        double xzFactor,
        double yFactor,
        double smearScaleMultiplier,
        string expectedPath)
    {
        Holder<DensityFunction>.Reference holder =
            BuiltInRegistries.DensityFunction.GetHolder(key) ??
            throw new InvalidOperationException($"Missing built-in {expectedPath} density-function holder.");

        Require(
            holder.IsBound &&
            holder.Key.Location.Path == expectedPath &&
            holder.Value is BlendedNoise
            {
                XzScale: var actualXzScale,
                YScale: var actualYScale,
                XzFactor: var actualXzFactor,
                YFactor: var actualYFactor,
                SmearScaleMultiplier: var actualSmearScaleMultiplier
            } &&
            actualXzScale == xzScale &&
            actualYScale == yScale &&
            actualXzFactor == xzFactor &&
            actualYFactor == yFactor &&
            actualSmearScaleMultiplier == smearScaleMultiplier,
            $"The {expectedPath} entry must use the vanilla old_blended_noise node and exact 26.3 parameters.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
