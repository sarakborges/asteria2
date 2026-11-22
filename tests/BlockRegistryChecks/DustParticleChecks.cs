using System.Numerics;
using System.Text.Json;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Util;

internal static class DustParticleChecks
{
    public static void Run()
    {
        CheckRegistrationOrder();
        CheckDustCodec();
        CheckDustColorTransitionCodec();
        CheckScaleConstruction();
    }

    private static void CheckRegistrationOrder()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;
        Require(registry.GetId(ParticleTypes.FALLING_WATER) < registry.GetId(ParticleTypes.DUST) &&
                registry.GetId(ParticleTypes.DUST) < registry.GetId(ParticleTypes.DUST_COLOR_TRANSITION) &&
                registry.GetId(ParticleTypes.DUST_COLOR_TRANSITION) < registry.GetId(ParticleTypes.EFFECT),
            "Dust and dust color transition must be registered after falling_water and before effect.");
        Require(!ParticleTypes.DUST.OverrideLimiter &&
                !ParticleTypes.DUST_COLOR_TRANSITION.OverrideLimiter,
            "Both dust particle types must retain overrideLimiter=false.");
    }

    private static void CheckDustCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        const int rgb = 0x4080C0;
        var source = new DustParticleOptions(rgb, 1.5F);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:dust" &&
                ops.Get(encoded, "color").GetOrThrow().GetInt32() == rgb &&
                ops.Get(encoded, "scale").GetOrThrow().GetSingle() == 1.5F,
            "Dust particle options must encode the discriminator, packed RGB color and scale.");

        DustParticleOptions decoded =
            (DustParticleOptions)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Vector3 decodedColor = decoded.GetColor();
        Require(decoded.Color == rgb &&
                decoded.GetScale() == 1.5F &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.DUST) &&
                MathF.Abs(decodedColor.X - (64.0F / 255.0F)) < 0.000001F &&
                MathF.Abs(decodedColor.Y - (128.0F / 255.0F)) < 0.000001F &&
                MathF.Abs(decodedColor.Z - (192.0F / 255.0F)) < 0.000001F,
            "Dust options must round-trip RGB and expose normalized color components.");

        JsonElement vectorInput = CreateOptionMap(
            ops,
            "asteria:dust",
            ("color", ops.CreateList(new[]
            {
                ops.CreateFloat(1.0F),
                ops.CreateFloat(0.5F),
                ops.CreateFloat(0.25F)
            })),
            ("scale", ops.CreateFloat(2.0F)));
        DustParticleOptions vectorDecoded =
            (DustParticleOptions)ParticleTypes.CODEC.Parse(ops, vectorInput).GetOrThrow();
        int vectorColor = Argb.ColorFromVector3(new Vector3(1.0F, 0.5F, 0.25F));
        Require(vectorDecoded.Color == vectorColor &&
                vectorDecoded.GetScale() == 2.0F,
            "Dust color must accept an RGB vector alternative.");

        JsonElement vectorReencoded =
            ParticleTypes.CODEC.EncodeStart(ops, vectorDecoded).GetOrThrow();
        Require(ops.Get(vectorReencoded, "color").GetOrThrow().GetInt32() == vectorColor,
            "Dust color vector alternatives must encode back to the primary integer form.");

        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(ops, "asteria:dust", ("scale", ops.CreateFloat(1.0F))))
                .IsError,
            "Dust codec must reject a missing required color field.");
        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(ops, "asteria:dust", ("color", ops.CreateInt(rgb))))
                .IsError,
            "Dust codec must reject a missing required scale field.");
        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(
                    ops,
                    "asteria:dust",
                    ("color", ops.CreateString("not_a_color")),
                    ("scale", ops.CreateFloat(1.0F))))
                .IsError,
            "Dust codec must reject an invalid color representation.");

        foreach (float scale in new[] { 0.0F, 0.009F, 4.001F, 5.0F })
        {
            JsonElement invalidScale = CreateOptionMap(
                ops,
                "asteria:dust",
                ("color", ops.CreateInt(rgb)),
                ("scale", ops.CreateFloat(scale)));
            Require(ParticleTypes.CODEC.Parse(ops, invalidScale).IsError,
                "Dust codec must reject scale values outside the inclusive [0.01;4.0] range.");
        }

        Require(ReferenceEquals(
                BuiltInRegistries.ParticleType.GetValue(
                    Asteria.Content.Identifiers.ResourceLocation.WithDefaultNamespace("dust")),
                ParticleTypes.DUST),
            "Dust options must be backed by the registered dust particle type.");
    }

    private static void CheckDustColorTransitionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        const int fromColor = 0x123456;
        const int toColor = 0xABCDEF;
        var source = new DustColorTransitionOptions(fromColor, toColor, 0.75F);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:dust_color_transition" &&
                ops.Get(encoded, "from_color").GetOrThrow().GetInt32() == fromColor &&
                ops.Get(encoded, "to_color").GetOrThrow().GetInt32() == toColor &&
                ops.Get(encoded, "scale").GetOrThrow().GetSingle() == 0.75F,
            "Dust transition options must encode both packed RGB colors and scale.");

        DustColorTransitionOptions decoded =
            (DustColorTransitionOptions)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.FromColor == fromColor &&
                decoded.ToColor == toColor &&
                decoded.GetScale() == 0.75F &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.DUST_COLOR_TRANSITION),
            "Dust transition options must round-trip both colors, scale and particle type.");

        Vector3 from = decoded.GetFromColor();
        Vector3 to = decoded.GetToColor();
        Require(MathF.Abs(from.X - (0x12 / 255.0F)) < 0.000001F &&
                MathF.Abs(from.Y - (0x34 / 255.0F)) < 0.000001F &&
                MathF.Abs(from.Z - (0x56 / 255.0F)) < 0.000001F &&
                MathF.Abs(to.X - (0xAB / 255.0F)) < 0.000001F &&
                MathF.Abs(to.Y - (0xCD / 255.0F)) < 0.000001F &&
                MathF.Abs(to.Z - (0xEF / 255.0F)) < 0.000001F,
            "Dust transition getters must expose normalized RGB vectors for each endpoint.");

        JsonElement vectorInput = CreateOptionMap(
            ops,
            "asteria:dust_color_transition",
            ("from_color", ops.CreateList(new[]
            {
                ops.CreateFloat(0.5F), ops.CreateFloat(0.25F), ops.CreateFloat(0.75F)
            })),
            ("to_color", ops.CreateList(new[]
            {
                ops.CreateFloat(1.0F), ops.CreateFloat(0.0F), ops.CreateFloat(0.5F)
            })),
            ("scale", ops.CreateFloat(1.25F)));
        DustColorTransitionOptions vectorDecoded =
            (DustColorTransitionOptions)ParticleTypes.CODEC.Parse(ops, vectorInput).GetOrThrow();
        Require(
            vectorDecoded.FromColor == Argb.ColorFromVector3(new Vector3(0.5F, 0.25F, 0.75F)) &&
            vectorDecoded.ToColor == Argb.ColorFromVector3(new Vector3(1.0F, 0.0F, 0.5F)) &&
            vectorDecoded.GetScale() == 1.25F,
            "Dust transition codec must accept RGB vector alternatives for both endpoint colors.");

        JsonElement vectorReencoded =
            ParticleTypes.CODEC.EncodeStart(ops, vectorDecoded).GetOrThrow();
        Require(ops.Get(vectorReencoded, "from_color").GetOrThrow().ValueKind == JsonValueKind.Number &&
                ops.Get(vectorReencoded, "to_color").GetOrThrow().ValueKind == JsonValueKind.Number,
            "Dust transition vector alternatives must encode to primary packed integer colors.");

        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(
                    ops,
                    "asteria:dust_color_transition",
                    ("to_color", ops.CreateInt(toColor)),
                    ("scale", ops.CreateFloat(1.0F))))
                .IsError,
            "Dust transition codec must reject a missing from_color field.");
        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(
                    ops,
                    "asteria:dust_color_transition",
                    ("from_color", ops.CreateInt(fromColor)),
                    ("scale", ops.CreateFloat(1.0F))))
                .IsError,
            "Dust transition codec must reject a missing to_color field.");
        Require(ParticleTypes.CODEC.Parse(
                ops,
                CreateOptionMap(
                    ops,
                    "asteria:dust_color_transition",
                    ("from_color", ops.CreateInt(fromColor)),
                    ("to_color", ops.CreateInt(toColor))))
                .IsError,
            "Dust transition codec must reject a missing scale field.");

        foreach (float scale in new[] { 0.0F, 0.009F, 4.001F, 5.0F })
        {
            JsonElement invalidScale = CreateOptionMap(
                ops,
                "asteria:dust_color_transition",
                ("from_color", ops.CreateInt(fromColor)),
                ("to_color", ops.CreateInt(toColor)),
                ("scale", ops.CreateFloat(scale)));
            Require(ParticleTypes.CODEC.Parse(ops, invalidScale).IsError,
                "Dust transition codec must reject scale outside the inclusive [0.01;4.0] range.");
        }

    }

    private static void CheckScaleConstruction()
    {
        var minimum = new DustParticleOptions(0xFFFFFF, 0.0F);
        var maximum = new DustParticleOptions(0xFFFFFF, 8.0F);
        var transitionMinimum = new DustColorTransitionOptions(0, 1, -2.0F);
        var transitionMaximum = new DustColorTransitionOptions(0, 1, 9.0F);

        Require(minimum.GetScale() == ScalableParticleOptionsBase.MIN_SCALE &&
                transitionMinimum.GetScale() == ScalableParticleOptionsBase.MIN_SCALE,
            "Scalable particle constructors must clamp values below the minimum scale.");
        Require(maximum.GetScale() == ScalableParticleOptionsBase.MAX_SCALE &&
                transitionMaximum.GetScale() == ScalableParticleOptionsBase.MAX_SCALE,
            "Scalable particle constructors must clamp values above the maximum scale.");

    }

    private static JsonElement CreateOptionMap(
        DynamicOps<JsonElement> ops,
        string type,
        params (string Name, JsonElement Value)[] fields)
    {
        var entries = new List<KeyValuePair<JsonElement, JsonElement>>(fields.Length + 1)
        {
            new(ops.CreateString("type"), ops.CreateString(type))
        };

        foreach ((string name, JsonElement value) in fields)
            entries.Add(new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString(name), value));

        return ops.CreateMap(entries);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
