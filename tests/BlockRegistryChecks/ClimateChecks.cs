using System.Text.Json;
using Asteria.Content.Biomes;
using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

internal static class ClimateChecks
{
    private sealed class CoordinateSampler(float scaleX, float scaleY, float scaleZ, float offset)
        : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume) =>
            DensitySampler.SampleVolumeNaive(context, outputBuffer, volume, this);

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ) =>
            offset + blockX * scaleX + blockY * scaleY + blockZ * scaleZ;
    }

    public static void Run()
    {
        CheckQuantization();
        CheckParametersAndFitness();
        CheckParameterCodecs();
        CheckParameterPointCodec();
        CheckParameterListCodec();
        CheckRTreeAgainstBruteForce();
        CheckClimateSampler();
    }

    private static void CheckQuantization()
    {
        Require(
            Climate.QuantizeCoord(1.23459F) == 12345L &&
            Climate.QuantizeCoord(-1.23459F) == -12345L,
            "Climate quantization must truncate float coordinates toward zero at a factor of 10000.");

        Require(
            Climate.UnquantizeCoord(12345L) == 1.2345F &&
            Climate.UnquantizeCoord(-12345L) == -1.2345F,
            "Climate unquantization must divide quantized coordinates by 10000.");

        Climate.TargetPoint target = Climate.Target(0.1F, -0.2F, 0.3F, -0.4F, 0.5F, -0.6F);
        Require(
            target == new Climate.TargetPoint(1000, -2000, 3000, -4000, 5000, -6000),
            "Climate.target must quantize the six climate axes in the vanilla field order.");
    }

    private static void CheckParametersAndFitness()
    {
        Climate.Parameter point = Climate.Parameter.Point(0.25F);
        Climate.Parameter span = Climate.Parameter.Span(-0.5F, 0.75F);
        Require(
            point == new Climate.Parameter(2500, 2500) &&
            span == new Climate.Parameter(-5000, 7500),
            "Climate.Parameter point and span factories must retain quantized inclusive bounds.");

        Require(
            span.Distance(-6000) == 1000 &&
            span.Distance(-5000) == 0 &&
            span.Distance(0) == 0 &&
            span.Distance(9000) == 1500,
            "Climate.Parameter distance must return zero inside the interval and distance to the nearest bound outside.");

        RequireThrows<ArgumentException>(
            () => Climate.Parameter.Span(0.5F, -0.5F),
            "Climate.Parameter.span must reject min greater than max.");

        Climate.Parameter merged = span.Span(Climate.Parameter.Span(-0.75F, 0.25F));
        Require(
            merged == new Climate.Parameter(-7500, 7500) &&
            span.Span(null) == span,
            "Climate.Parameter.span(other) must union interval bounds and preserve the receiver when other is null.");

        Climate.ParameterPoint pointA = Climate.Parameters(
            0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F);
        Climate.ParameterPoint pointB = Climate.Parameters(
            0.1F, -0.1F, 0.2F, -0.2F, 0.3F, -0.3F, 0.25F);
        Climate.TargetPoint target = Climate.Target(0.1F, -0.1F, 0.2F, -0.2F, 0.3F, -0.3F);

        Require(
            pointB.Fitness(target) == 6250000L &&
            pointA.Fitness(target) > pointB.Fitness(target),
            "Climate.ParameterPoint.fitness must sum squared distance on six axes plus squared offset.");
    }

    private static void CheckParameterCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Climate.Parameter parameter = Climate.Parameter.Span(-0.75F, 1.25F);
        JsonElement encoded = Climate.Parameter.CODEC.EncodeStart(ops, parameter).GetOrThrow();

        Require(
            Math.Abs(ops.Get(encoded, "min").GetOrThrow().GetSingle() + 0.75F) < 1.0E-6F &&
            Math.Abs(ops.Get(encoded, "max").GetOrThrow().GetSingle() - 1.25F) < 1.0E-6F,
            "Climate.Parameter codec must use min/max float fields.");

        Climate.Parameter decoded = Climate.Parameter.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded == parameter,
            "Climate.Parameter codec must round-trip its quantized interval.");

        Require(
            Climate.Parameter.CODEC.Parse(
                ops,
                ParseJson("""{"min":0.5,"max":-0.5}""")).IsError,
            "Climate.Parameter codec must reject intervals whose minimum exceeds their maximum.");

        Require(
            Climate.Parameter.CODEC.Parse(
                ops,
                ParseJson("""{"min":-2.1,"max":1.0}""")).IsError &&
            Climate.Parameter.CODEC.Parse(
                ops,
                ParseJson("""{"min":-1.0,"max":2.1}""")).IsError,
            "Climate.Parameter codec must enforce the [-2, 2] coordinate bounds.");
    }

    private static void CheckParameterPointCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Climate.ParameterPoint expected = Climate.Parameters(
            Climate.Parameter.Span(-0.5F, 0.25F),
            Climate.Parameter.Point(0.1F),
            Climate.Parameter.Span(-1.0F, -0.25F),
            Climate.Parameter.Point(0.7F),
            Climate.Parameter.Span(0.0F, 0.5F),
            Climate.Parameter.Point(-0.8F),
            0.125F);

        JsonElement encoded = Climate.ParameterPoint.CODEC.EncodeStart(ops, expected).GetOrThrow();
        string[] fields =
        {
            "temperature", "humidity", "continentalness",
            "erosion", "depth", "weirdness", "offset"
        };

        foreach (string field in fields)
            Require(ops.Get(encoded, field).IsSuccess, $"Climate.ParameterPoint codec must write '{field}'.");

        Climate.ParameterPoint decoded =
            Climate.ParameterPoint.CODEC.Parse(ops, encoded).GetOrThrow();

        Require(
            decoded == expected,
            "Climate.ParameterPoint codec must round-trip all six climate intervals and the quantized offset.");

        Require(
            Climate.ParameterPoint.CODEC.Parse(
                ops,
                ParseJson("""{"temperature":{"min":0,"max":0},"humidity":{"min":0,"max":0},"continentalness":{"min":0,"max":0},"erosion":{"min":0,"max":0},"depth":{"min":0,"max":0},"weirdness":{"min":0,"max":0},"offset":1.1}""")).IsError,
            "Climate.ParameterPoint codec must reject offsets outside [0, 1].");

        Require(
            Climate.ParameterPoint.CODEC.Parse(
                ops,
                ParseJson("""{"temperature":{"min":0,"max":0}}""")).IsError,
            "Climate.ParameterPoint codec must report missing required climate dimensions as a DataResult error.");
    }

    private static void CheckParameterListCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<Climate.ParameterList<string>> codec =
            Climate.ParameterList<string>.CreateCodec(Codecs.STRING.FieldOf("biome"));

        var values = new Climate.ParameterList<string>(
        [
            new(
                Climate.Parameters(0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F, 0.0F),
                "asteria:plains"),
            new(
                Climate.Parameters(1.0F, 0.5F, -0.5F, 0.2F, 0.0F, -0.8F, 0.1F),
                "asteria:desert")
        ]);

        JsonElement encoded = codec.EncodeStart(ops, values).GetOrThrow();
        Require(
            encoded.ValueKind == JsonValueKind.Array &&
            encoded.GetArrayLength() == 2 &&
            ops.Get(encoded[0], "biome").GetOrThrow().GetString() == "asteria:plains",
            "Climate.ParameterList codec must serialize a non-empty list of parameter/value records.");

        Climate.ParameterList<string> decoded = codec.Parse(ops, encoded).GetOrThrow();
        Require(
            decoded.Values().Count == 2 &&
            decoded.Values()[0].Second == "asteria:plains" &&
            decoded.Values()[1].Second == "asteria:desert" &&
            decoded.Values()[0].First == values.Values()[0].First,
            "Climate.ParameterList codec must restore ordered parameter/value entries.");

        Require(
            codec.Parse(ops, ParseJson("[]")).IsError,
            "Climate.ParameterList codec must reject an empty list.");
    }

    private static void CheckRTreeAgainstBruteForce()
    {
        var values = new List<Pair<Climate.ParameterPoint, string>>();
        for (int index = 0; index < 64; index++)
        {
            float x = ((index * 37) % 127 - 63) / 50.0F;
            float y = ((index * 53) % 131 - 65) / 52.0F;
            float z = ((index * 29) % 113 - 56) / 48.0F;
            float erosion = ((index * 71) % 137 - 68) / 55.0F;
            float depth = ((index * 43) % 109 - 54) / 50.0F;
            float weirdness = ((index * 61) % 139 - 69) / 55.0F;
            float offset = ((index * 17) % 31) / 100.0F;

            Climate.ParameterPoint point = Climate.Parameters(
                x, y, z, erosion, depth, weirdness, offset);
            values.Add(new Pair<Climate.ParameterPoint, string>(point, $"biome-{index}"));
        }

        for (int childrenPerNodeIndex = 0; childrenPerNodeIndex < 5; childrenPerNodeIndex++)
        {
            int childrenPerNode = new[] { 2, 3, 5, 19, 64 }[childrenPerNodeIndex];
            Climate.ParameterList<string> indexed =
                new Climate.ParameterList<string>(values).RebuildWithChildrenPerNode(childrenPerNode);

            for (int queryIndex = 0; queryIndex < 100; queryIndex++)
            {
                float x = ((queryIndex * 31) % 101 - 50) / 45.0F;
                float y = ((queryIndex * 47) % 103 - 51) / 48.0F;
                float z = ((queryIndex * 19) % 97 - 48) / 42.0F;
                float erosion = ((queryIndex * 59) % 107 - 53) / 50.0F;
                float depth = ((queryIndex * 23) % 89 - 44) / 40.0F;
                float weirdness = ((queryIndex * 41) % 99 - 49) / 47.0F;

                Climate.TargetPoint target = Climate.Target(x, y, z, erosion, depth, weirdness);
                string expected = indexed.FindValueBruteForce(target);
                string actual = indexed.FindValue(target);

                Require(
                    actual == expected,
                    $"Climate R-tree search must match brute force for node fanout {childrenPerNode}, query {queryIndex}.");
            }
        }
    }

    private static void CheckClimateSampler()
    {
        var sampler = new Climate.Sampler(
            new CoordinateSampler(1.0F, 0.0F, 0.0F, 1.0F).Bind(SamplerContext.EmptyUncached),
            new CoordinateSampler(0.0F, 1.0F, 0.0F, 2.0F).Bind(SamplerContext.EmptyUncached),
            new CoordinateSampler(0.0F, 0.0F, 1.0F, 3.0F).Bind(SamplerContext.EmptyUncached),
            new CoordinateSampler(1.0F, 1.0F, 0.0F, 4.0F).Bind(SamplerContext.EmptyUncached),
            new CoordinateSampler(0.0F, 1.0F, 1.0F, 5.0F).Bind(SamplerContext.EmptyUncached),
            new CoordinateSampler(1.0F, 0.0F, 1.0F, 6.0F).Bind(SamplerContext.EmptyUncached));

        Climate.TargetPoint target = sampler.Sample(-2, 5, 3);
        Require(
            target == Climate.Target(-7.0F, 22.0F, 15.0F, 16.0F, 37.0F, 10.0F),
            "Climate.Sampler must sample all six bound density samplers at the block coordinates represented by quart positions.");
    }

    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
