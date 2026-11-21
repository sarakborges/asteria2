using System.Numerics;
using System.Text.Json;
using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.Attributes;
using Asteria.World.Level;

internal static class AttributeValueCodecChecks
{
    public static void Run()
    {
        CheckEveryAttributeTypeHasCodec();
        CheckPrimitiveAttributeCodecs();
        CheckMoonPhaseCodec();
        CheckRgbColorCodec();
        CheckArgbColorCodec();
        CheckFixedSizeVectorsAndHexErrors();
        CheckAlternativePartialResultPrecedence();
    }

    private static void CheckEveryAttributeTypeHasCodec()
    {
        Require(AttributeTypes.BOOLEAN.ValueCodec is not null &&
                AttributeTypes.FLOAT.ValueCodec is not null &&
                AttributeTypes.ANGLE_DEGREES.ValueCodec is not null &&
                AttributeTypes.INTEGER.ValueCodec is not null &&
                AttributeTypes.MOON_PHASE.ValueCodec is not null &&
                AttributeTypes.RGB_COLOR.ValueCodec is not null &&
                AttributeTypes.ARGB_COLOR.ValueCodec is not null &&
                AttributeTypes.PARTICLE.ValueCodec is not null &&
                AttributeTypes.AMBIENT_PARTICLES.ValueCodec is not null,
            "All nine built-in attribute types must expose their value codecs.");
    }

    private static void CheckPrimitiveAttributeCodecs()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        Require(AttributeTypes.BOOLEAN.ValueCodec!.Parse(ops, ops.CreateBoolean(true)).GetOrThrow(),
            "Boolean attribute values must use the native boolean codec.");
        Require(AttributeTypes.FLOAT.ValueCodec!.Parse(ops, ops.CreateFloat(0.75F)).GetOrThrow() == 0.75F,
            "Float attribute values must use the native float codec.");
        Require(AttributeTypes.ANGLE_DEGREES.ValueCodec!.Parse(ops, ops.CreateFloat(180.0F)).GetOrThrow() == 180.0F,
            "Angle attribute values must use the native float codec.");
        Require(AttributeTypes.INTEGER.ValueCodec!.Parse(ops, ops.CreateInt(-42)).GetOrThrow() == -42,
            "Integer attribute values must use the native integer codec.");
    }

    private static void CheckMoonPhaseCodec()
    {
        DynamicOps<JsonElement> strict = JsonOps.INSTANCE;
        DynamicOps<JsonElement> compressed = JsonOps.COMPRESSED;

        JsonElement encoded = MoonPhaseExtensions.CODEC.EncodeStart(strict, MoonPhase.WANING_GIBBOUS).GetOrThrow();
        Require(strict.GetStringValue(encoded).GetOrThrow() == "waning_gibbous",
            "Moon phases must serialize by their stable string names.");
        Require(MoonPhaseExtensions.CODEC.Parse(strict, encoded).GetOrThrow() == MoonPhase.WANING_GIBBOUS,
            "Moon phase names must decode to their enum values.");
        Require(MoonPhaseExtensions.CODEC.Parse(strict, strict.CreateString("WANING_GIBBOUS")).IsError,
            "Moon phase names must be case-sensitive.");
        Require(MoonPhaseExtensions.CODEC.Parse(strict, strict.CreateInt(1)).IsError,
            "Strict DynamicOps must reject numeric moon phase IDs.");

        JsonElement compressedId = MoonPhaseExtensions.CODEC.EncodeStart(compressed, MoonPhase.WAXING_GIBBOUS).GetOrThrow();
        Require(compressed.GetNumberValue(compressedId).GetOrThrow().IntValue == 7 &&
                MoonPhaseExtensions.CODEC.Parse(compressed, compressedId).GetOrThrow() == MoonPhase.WAXING_GIBBOUS,
            "Compressed DynamicOps must encode and decode moon phase ordinal IDs.");
        Require(MoonPhaseExtensions.CODEC.Parse(compressed, compressed.CreateInt(8)).IsError,
            "Compressed moon phase IDs outside the enum range must be rejected.");
    }

    private static void CheckRgbColorCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Vector3 source = new(1.0F, 0.5F, 0.0F);

        JsonElement encoded = AttributeTypes.RGB_COLOR.ValueCodec!.EncodeStart(ops, source).GetOrThrow();
        Require(ops.GetStringValue(encoded).GetOrThrow() == "#ff7f00",
            "RGB vector encoding must use a six-digit lowercase hex string and floor channel conversion.");

        Vector3 decoded = AttributeTypes.RGB_COLOR.ValueCodec.Parse(ops, encoded).GetOrThrow();
        Require(decoded.X == 1.0F &&
                Math.Abs(decoded.Y - (127.0F / 255.0F)) < 0.000001F &&
                decoded.Z == 0.0F,
            "RGB hex decoding must expand 8-bit channels into normalized vector components.");

        Vector3 fromArray = AttributeTypes.RGB_COLOR.ValueCodec.Parse(
            ops,
            ops.CreateList(new[] { ops.CreateFloat(0.25F), ops.CreateFloat(0.5F), ops.CreateFloat(0.75F) }))
            .GetOrThrow();
        Require(fromArray == new Vector3(0.25F, 0.5F, 0.75F),
            "RGB codecs must accept three-element float arrays as an alternative.");

        Vector3 fromInteger = AttributeTypes.RGB_COLOR.ValueCodec.Parse(
            ops, ops.CreateInt(0x00804020)).GetOrThrow();
        Require(Math.Abs(fromInteger.X - (128.0F / 255.0F)) < 0.000001F &&
                Math.Abs(fromInteger.Y - (64.0F / 255.0F)) < 0.000001F &&
                Math.Abs(fromInteger.Z - (32.0F / 255.0F)) < 0.000001F,
            "RGB codecs must accept packed integer colors as an alternative.");
    }

    private static void CheckArgbColorCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Vector4 source = new(1.0F, 0.5F, 0.0F, 0.25F);

        JsonElement encoded = AttributeTypes.ARGB_COLOR.ValueCodec!.EncodeStart(ops, source).GetOrThrow();
        Require(ops.GetStringValue(encoded).GetOrThrow() == "#3fff7f00",
            "ARGB vector encoding must put alpha first and floor each channel.");

        Vector4 decoded = AttributeTypes.ARGB_COLOR.ValueCodec.Parse(ops, encoded).GetOrThrow();
        Require(decoded.X == 1.0F &&
                Math.Abs(decoded.Y - (127.0F / 255.0F)) < 0.000001F &&
                decoded.Z == 0.0F &&
                Math.Abs(decoded.W - (63.0F / 255.0F)) < 0.000001F,
            "ARGB hex decoding must normalize red, green, blue and alpha channels.");

        Vector4 fromArray = AttributeTypes.ARGB_COLOR.ValueCodec.Parse(
            ops,
            ops.CreateList(new[]
            {
                ops.CreateFloat(0.1F), ops.CreateFloat(0.2F),
                ops.CreateFloat(0.3F), ops.CreateFloat(0.4F)
            })).GetOrThrow();
        Require(fromArray == new Vector4(0.1F, 0.2F, 0.3F, 0.4F),
            "ARGB codecs must accept four-element float arrays as an alternative.");

        Vector4 fromInteger = AttributeTypes.ARGB_COLOR.ValueCodec.Parse(
            ops, ops.CreateInt(unchecked((int)0x80402010u))).GetOrThrow();
        Require(Math.Abs(fromInteger.X - (64.0F / 255.0F)) < 0.000001F &&
                Math.Abs(fromInteger.Y - (32.0F / 255.0F)) < 0.000001F &&
                Math.Abs(fromInteger.Z - (16.0F / 255.0F)) < 0.000001F &&
                Math.Abs(fromInteger.W - (128.0F / 255.0F)) < 0.000001F,
            "ARGB codecs must accept packed alpha-red-green-blue integers as an alternative.");
    }

    private static void CheckFixedSizeVectorsAndHexErrors()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<Vector3> rgb = AttributeTypes.RGB_COLOR.ValueCodec!;
        Codec<Vector4> argb = AttributeTypes.ARGB_COLOR.ValueCodec!;

        Require(rgb.Parse(ops, ops.CreateList(new[] { ops.CreateFloat(0.1F), ops.CreateFloat(0.2F) })).IsError,
            "RGB arrays with fewer than three elements must fail.");
        Require(argb.Parse(ops, ops.CreateList(new[] {
                ops.CreateFloat(0.1F), ops.CreateFloat(0.2F), ops.CreateFloat(0.3F) })).IsError,
            "ARGB arrays with fewer than four elements must fail.");

        Require(rgb.Parse(ops, ops.CreateString("ffffff")).IsError,
            "Hex colors without a leading # must fail.");
        Require(rgb.Parse(ops, ops.CreateString("#fff")).IsError,
            "Hex colors with the wrong number of digits must fail.");
        Require(argb.Parse(ops, ops.CreateString("#xyzxyz00")).IsError,
            "Hex colors with non-hex digits must fail.");

        DataResult<Vector3> oversized = ExtraCodecs.VECTOR3F.Parse(
            ops,
            ops.CreateList(new[]
            {
                ops.CreateFloat(0.1F), ops.CreateFloat(0.2F),
                ops.CreateFloat(0.3F), ops.CreateFloat(0.4F)
            }));
        Require(oversized.IsError && oversized.TryGetPartial(out Vector3 partial) &&
                partial == new Vector3(0.1F, 0.2F, 0.3F),
            "Fixed-size vector decoding must preserve the first expected elements as a partial result when the list is oversized.");
    }

    private static void CheckAlternativePartialResultPrecedence()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<int> primary = Codecs.INT.Validate(value =>
            value >= 0
                ? DataResult<int>.Success(value)
                : DataResult<int>.Error("negative", 5));

        Codec<string> successfulAlternative = Codecs.INT.XMap(
            static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            static value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        Codec<int> withSuccessfulAlternative =
            primary.WithAlternative(successfulAlternative, static value => value.Length);

        DataResult<int> successfulResult =
            withSuccessfulAlternative.Parse(ops, ops.CreateInt(-1));
        Require(successfulResult.IsSuccess && successfulResult.GetOrThrow() == 2,
            "A successful alternative must take precedence over a partial error from the primary codec.");

        Codec<int> partialAlternative = Codecs.INT.Validate(_ =>
            DataResult<int>.Error("alternative", 9));
        Codec<int> withTwoPartialErrors =
            primary.WithAlternative(partialAlternative, static value => value);

        DataResult<int> partialResult =
            withTwoPartialErrors.Parse(ops, ops.CreateInt(-1));
        Require(partialResult.IsError &&
                partialResult.TryGetPartial(out int partial) &&
                partial == 5 &&
                partialResult.ErrorMessage == "negative",
            "When both alternatives fail with partial values, the first codec's partial result must win.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
