using System.Text.Json;
using Asteria.Content.Biomes;
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
