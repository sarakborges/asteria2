using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class SpawnTargetPointChecks
{
    public static void Run()
    {
        CheckFitnessUsesAllDensityAxes();
        CheckFitnessUsesQuantizedSquaredDistance();
        CheckRegistryBackedMapCodec();
    }

    private static void CheckFitnessUsesAllDensityAxes()
    {
        var temperature = new ConstantFunction(0.5f);
        var erosion = new ConstantFunction(-0.25f);
        var parameters = new Dictionary<Holder<DensityFunction>, Climate.Parameter>
        {
            [Holder<DensityFunction>.Direct(temperature)] = Climate.Parameter.Point(0.5f),
            [Holder<DensityFunction>.Direct(erosion)] = Climate.Parameter.Span(-0.5f, 0.5f)
        };
        var target = new SpawnTargetPoint(parameters);

        long fitness = target.SampleFitness(
            ConstantSamplerSet.Instance,
            blockX: 17,
            blockY: 64,
            blockZ: -29);

        Require(
            fitness == 0L,
            "SpawnTargetPoint must support arbitrary density-function axes and produce zero fitness inside every target interval.");
    }

    private static void CheckFitnessUsesQuantizedSquaredDistance()
    {
        var density = new ConstantFunction(0.5f);
        var target = new SpawnTargetPoint(
            new Dictionary<Holder<DensityFunction>, Climate.Parameter>
            {
                [Holder<DensityFunction>.Direct(density)] =
                    Climate.Parameter.Point(0.25f)
            });

        long fitness = target.SampleFitness(
            ConstantSamplerSet.Instance,
            blockX: 0,
            blockY: 0,
            blockZ: 0);

        Require(
            fitness == 6_250_000L,
            "SpawnTargetPoint must quantize the sampled value before squaring the Climate.Parameter distance.");
    }

    private static void CheckRegistryBackedMapCodec()
    {
        DynamicOps<JsonElement> jsonOps = JsonOps.INSTANCE;
        var registry = new Registry<DensityFunction>(Registries.DENSITY_FUNCTION.Location);

        RegistryKey<DensityFunction> temperatureKey = RegistryKey<DensityFunction>.Create(
            Registries.DENSITY_FUNCTION,
            ResourceLocation.Parse("asteria:spawn_temperature"));
        RegistryKey<DensityFunction> erosionKey = RegistryKey<DensityFunction>.Create(
            Registries.DENSITY_FUNCTION,
            ResourceLocation.Parse("asteria:spawn_erosion"));

        var temperature = new ConstantFunction(0.375f);
        var erosion = new ConstantFunction(-0.125f);
        Holder<DensityFunction>.Reference temperatureHolder = registry.Register(
            temperatureKey,
            temperature,
            RegistrationInfo.BuiltIn);
        Holder<DensityFunction>.Reference erosionHolder = registry.Register(
            erosionKey,
            erosion,
            RegistrationInfo.BuiltIn);
        registry.Freeze();

        HolderLookupProvider lookupProvider =
            HolderLookupProvider.Create(new IRegistryLookup[] { registry });
        RegistryOps<JsonElement> registryOps =
            RegistryOps<JsonElement>.Create(jsonOps, lookupProvider);

        var expected = new SpawnTargetPoint(
            new Dictionary<Holder<DensityFunction>, Climate.Parameter>
            {
                [temperatureHolder] = Climate.Parameter.Point(0.375f),
                [erosionHolder] = Climate.Parameter.Span(-0.25f, 0.125f)
            });

        JsonElement encoded = SpawnTargetPoint.CODEC
            .EncodeStart(registryOps, expected)
            .GetOrThrow();

        Require(
            encoded.ValueKind == JsonValueKind.Object &&
            registryOps.GetMapValues(encoded).GetOrThrow().Count == 2,
            "SpawnTargetPoint CODEC must encode an unbounded map with one entry per registered density-function axis.");

        Require(
            registryOps.Get(encoded, "asteria:spawn_temperature").IsSuccess &&
            registryOps.Get(encoded, "asteria:spawn_erosion").IsSuccess,
            "SpawnTargetPoint map keys must be the density-function registry identifiers.");

        SpawnTargetPoint decoded = SpawnTargetPoint.CODEC
            .Parse(registryOps, encoded)
            .GetOrThrow();

        Require(
            decoded.Parameters.Count == 2 &&
            decoded.Parameters.TryGetValue(temperatureHolder, out Climate.Parameter temperatureRange) &&
            temperatureRange == Climate.Parameter.Point(0.375f) &&
            decoded.Parameters.TryGetValue(erosionHolder, out Climate.Parameter erosionRange) &&
            erosionRange == Climate.Parameter.Span(-0.25f, 0.125f),
            "SpawnTargetPoint CODEC must resolve registry holders and preserve every Climate.Parameter interval.");

        Require(
            SpawnTargetPoint.CODEC.Parse(jsonOps, encoded).IsError,
            "SpawnTargetPoint CODEC must reject registry identifiers when no density-function registry context is available.");
    }

    private sealed class ConstantSamplerSet : DensitySamplerSet
    {
        public static ConstantSamplerSet Instance { get; } = new();

        public DensitySampler.Bound Get(DensityFunction function)
        {
            if (function is not ConstantFunction constant)
                throw new InvalidOperationException(
                    "The SpawnTargetPoint constant sampler test only accepts ConstantFunction nodes.");

            return new ConstantFunction.Sampler(constant.Value)
                .Bind(SamplerContext.EmptyUncached);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
