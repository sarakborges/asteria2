using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class NoiseRouterChecks
{
    private sealed class ConstantDensitySamplerSet : DensitySamplerSet
    {
        public List<DensityFunction> Requested { get; } = [];

        public DensitySampler.Bound Get(DensityFunction function)
        {
            Requested.Add(function);
            if (function is not ConstantFunction constant)
                throw new InvalidOperationException(
                    $"NoiseRouter test expected constant density nodes, received {function.GetType().Name}.");

            return new DensitySampler.Bound(
                new ConstantFunction.Sampler(constant.Value),
                SamplerContext.EmptyUncached);
        }
    }

    public static void Run()
    {
        CheckCodecRoundTrip();
        CheckClimateSamplerMapping();
        CheckOverworldFunctionSetMapping();
        CheckRegistryBackedOverworldProfiles();
    }

    private static NoiseRouter CreateRouter() =>
        new(
            new ConstantFunction(0.125f),
            new ConstantFunction(-0.25f),
            new ConstantFunction(0.375f),
            new ConstantFunction(-0.5f),
            new ConstantFunction(0.625f),
            new ConstantFunction(-0.75f),
            new ConstantFunction(0.875f),
            new ConstantFunction(-1.0f));

    private static void CheckOverworldFunctionSetMapping()
    {
        var functions = new OverworldFunctionSet<DensityFunction>(
            new ConstantFunction(1.0f),
            new ConstantFunction(2.0f),
            new ConstantFunction(3.0f),
            new ConstantFunction(4.0f),
            new ConstantFunction(5.0f),
            new ConstantFunction(6.0f),
            new ConstantFunction(7.0f),
            new ConstantFunction(8.0f),
            new ConstantFunction(9.0f),
            new ConstantFunction(10.0f),
            new ConstantFunction(11.0f),
            new ConstantFunction(12.0f));
        DensityFunction ridges = new ConstantFunction(13.0f);

        NoiseRouter router = NoiseRouter.CreateOverworld(functions, ridges);

        Require(
            ReferenceEquals(router.Temperature, functions.Temperature) &&
            ReferenceEquals(router.Vegetation, functions.Vegetation) &&
            ReferenceEquals(router.Continents, functions.Continents) &&
            ReferenceEquals(router.Erosion, functions.Erosion) &&
            ReferenceEquals(router.Depth, functions.Depth) &&
            ReferenceEquals(router.Ridges, ridges) &&
            ReferenceEquals(router.ChunkSurfaceLevel, functions.ChunkSurfaceLevel) &&
            ReferenceEquals(router.FinalDensity, functions.FinalDensity),
            "NoiseRouter.createOverworld must map the twelve-slot function set to the exact eight vanilla router channels.");

        Require(
            router.Ridges is ConstantFunction { Value: 13.0f } &&
            router.ChunkSurfaceLevel is ConstantFunction { Value: 11.0f } &&
            router.FinalDensity is ConstantFunction { Value: 12.0f },
            "NoiseRouter.createOverworld must take ridges from the shared ridge function and retain chunk_surface_level/final_density from the function set.");
    }

    private static void CheckRegistryBackedOverworldProfiles()
    {
        Registry<DensityFunction> registry = BuiltInRegistries.DensityFunction;

        CheckProfile(
            registry,
            DensityFunctionKeys.OVERWORLD_FUNCTIONS);
        CheckProfile(
            registry,
            DensityFunctionKeys.LARGE_OVERWORLD_FUNCTIONS);
        CheckProfile(
            registry,
            DensityFunctionKeys.AMPLIFIED_OVERWORLD_FUNCTIONS);
    }

    private static void CheckProfile(
        Registry<DensityFunction> registry,
        OverworldFunctionSet<RegistryKey<DensityFunction>> names)
    {
        NoiseRouter router = NoiseRouter.CreateOverworld(registry, names);

        Require(
            IsRegisteredFunction(router.Temperature, registry, names.Temperature) &&
            IsRegisteredFunction(router.Vegetation, registry, names.Vegetation) &&
            IsRegisteredFunction(router.Continents, registry, names.Continents) &&
            IsRegisteredFunction(router.Erosion, registry, names.Erosion) &&
            IsRegisteredFunction(router.Depth, registry, names.Depth) &&
            IsRegisteredFunction(router.ChunkSurfaceLevel, registry, names.ChunkSurfaceLevel) &&
            IsRegisteredFunction(router.FinalDensity, registry, names.FinalDensity),
            "Registry-backed Overworld routing must select the matching climate, depth, chunk-surface and final-density holders from the supplied profile.");

        Require(
            IsRegisteredFunction(router.Ridges, registry, DensityFunctionKeys.RIDGES) &&
            !IsRegisteredFunction(router.Ridges, registry, DensityFunctionKeys.RIDGES_FOLDED),
            "The vanilla NoiseRouter.ridges channel must resolve overworld/ridges, not overworld/ridges_folded.");
    }

    private static bool IsRegisteredFunction(
        DensityFunction function,
        Registry<DensityFunction> registry,
        RegistryKey<DensityFunction> key) =>
        function is DensityFunctions.HolderHolder { Function: var holder } &&
        ReferenceEquals(holder, registry.GetHolder(key));

    private static void CheckCodecRoundTrip()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        NoiseRouter expected = CreateRouter();

        JsonElement encoded = NoiseRouter.CODEC
            .EncodeStart(ops, expected)
            .GetOrThrow();

        string[] fields =
        {
            "temperature",
            "vegetation",
            "continents",
            "erosion",
            "depth",
            "ridges",
            "chunk_surface_level",
            "final_density"
        };

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            ops.GetMapValues(encoded).GetOrThrow().Count == fields.Length,
            "NoiseRouter must encode exactly its eight 26.3 density-function fields.");

        float[] values =
        {
            0.125f,
            -0.25f,
            0.375f,
            -0.5f,
            0.625f,
            -0.75f,
            0.875f,
            -1.0f
        };

        for (int index = 0; index < fields.Length; index++)
        {
            Require(
                ops.Get(encoded, fields[index]).GetOrThrow().GetSingle() == values[index],
                $"NoiseRouter codec must preserve the {fields[index]} density function.");
        }

        NoiseRouter decoded = NoiseRouter.CODEC
            .Parse(ops, encoded)
            .GetOrThrow();

        Require(
            decoded == expected,
            "NoiseRouter must round-trip all eight density-function channels.");

        JsonElement missingField = ParseJson(
            """{"temperature":0.0,"vegetation":0.0,"continents":0.0,"erosion":0.0,"depth":0.0,"ridges":0.0,"chunk_surface_level":0.0}""");
        Require(
            NoiseRouter.CODEC.Parse(ops, missingField).IsError,
            "NoiseRouter codec must reject a missing required final_density field.");

        JsonElement wrongFieldType = ParseJson(
            """{"temperature":0.0,"vegetation":0.0,"continents":0.0,"erosion":0.0,"depth":0.0,"ridges":0.0,"chunk_surface_level":0.0,"final_density":{}}""");
        Require(
            NoiseRouter.CODEC.Parse(ops, wrongFieldType).IsError,
            "NoiseRouter codec must reject a malformed density-function field.");
    }

    private static void CheckClimateSamplerMapping()
    {
        NoiseRouter router = CreateRouter();
        var samplers = new ConstantDensitySamplerSet();

        Climate.Sampler climateSampler = router.CreateClimateSampler(samplers);

        Require(
            samplers.Requested.SequenceEqual(new DensityFunction[]
            {
                router.Temperature,
                router.Vegetation,
                router.Continents,
                router.Erosion,
                router.Depth,
                router.Ridges
            }),
            "NoiseRouter.createClimateSampler must request the six climate channels in vanilla field order and omit terrain-only channels.");

        Climate.TargetPoint sampled = climateSampler.Sample(-2, 5, 3);
        Require(
            sampled == Climate.Target(
                0.125f,
                -0.25f,
                0.375f,
                -0.5f,
                0.625f,
                -0.75f),
            "NoiseRouter climate sampler must map vegetation to humidity and ridges to weirdness.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
