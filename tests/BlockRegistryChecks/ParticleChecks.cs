using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Util;

internal static class ParticleChecks
{
    public static void Run()
    {
        CheckRegistry();
        CheckSimpleOptionsCodec();
        CheckColorParticleOptionsCodec();
        CheckByNameCodecFailures();
    }

    private static void CheckRegistry()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;

        Require(registry.IsFrozen,
            "The particle type registry must be frozen after built-in bootstrap.");
        Require(registry.Count == ParticleTypes.RegisteredTypeCount,
            "Every implemented simple particle type must be registered.");
        Require(ReferenceEquals(
                registry.GetValue(ResourceLocation.WithDefaultNamespace("angry_villager")),
                ParticleTypes.ANGRY_VILLAGER),
            "The first simple particle type must use its source identifier.");
        Require(ReferenceEquals(
                registry.GetValue(ResourceLocation.WithDefaultNamespace("trial_spawner_detection")),
                ParticleTypes.TRIAL_SPAWNER_DETECTED_PLAYER),
            "The trial-spawner player particle must keep its non-field registry identifier.");
        Require(registry.GetId(ParticleTypes.ASH) < registry.GetId(ParticleTypes.FIREFLY),
            "Simple particle registration order must follow the source declaration order.");

        Require(ParticleTypes.DAMAGE_INDICATOR.OverrideLimiter &&
                ParticleTypes.EXPLOSION.OverrideLimiter &&
                ParticleTypes.POOF.OverrideLimiter &&
                ParticleTypes.GLOW.OverrideLimiter,
            "Particles marked overrideLimiter=true must retain that flag.");
        Require(!ParticleTypes.BUBBLE.OverrideLimiter &&
                !ParticleTypes.ASH.OverrideLimiter &&
                !ParticleTypes.FIREFLY.OverrideLimiter,
            "Particles marked overrideLimiter=false must retain that flag.");
    }

    private static void CheckSimpleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        IParticleOptions source = ParticleTypes.FIREFLY;

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.Object &&
                ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:firefly",
            "The particle options codec must encode the registered type key.");

        IParticleOptions decoded = ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded, ParticleTypes.FIREFLY),
            "A simple particle options codec must decode to the registered singleton instance.");
        Require(ReferenceEquals(decoded.GetParticleType(), ParticleTypes.FIREFLY),
            "Simple particle options must report their own particle type.");
    }

    private static void CheckColorParticleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        int packedColor = unchecked((int)0x80402010u);
        ColorParticleOption source = ColorParticleOption.Create(ParticleTypes.ENTITY_EFFECT, packedColor);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:entity_effect" &&
                ops.Get(encoded, "color").GetOrThrow().GetInt32() == packedColor,
            "Color particle options must serialize their registered discriminator and packed ARGB integer.");

        IParticleOptions decodedOptions = ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decodedOptions is ColorParticleOption decoded &&
                ReferenceEquals(decoded.Type, ParticleTypes.ENTITY_EFFECT) &&
                decoded.Color == packedColor,
            "Color particle options must round-trip to their concrete registered type.");

        Require(Math.Abs(decoded.GetRed() - (64.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetGreen() - (32.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetBlue() - (16.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetAlpha() - (128.0F / 255.0F)) < 0.000001F,
            "Color particle channel accessors must return normalized 8-bit ARGB channels.");

        JsonElement vectorOption = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"),
                ops.CreateString("asteria:flash")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("color"),
                ops.CreateList(new[]
                {
                    ops.CreateFloat(1.0F),
                    ops.CreateFloat(0.5F),
                    ops.CreateFloat(0.25F),
                    ops.CreateFloat(0.75F)
                }))
        });

        ColorParticleOption vectorDecoded =
            (ColorParticleOption)ParticleTypes.CODEC.Parse(ops, vectorOption).GetOrThrow();
        int expectedColor = Argb.Color(191, 255, 127, 63);
        Require(vectorDecoded.Color == expectedColor &&
                ReferenceEquals(vectorDecoded.Type, ParticleTypes.FLASH),
            "Color particle codec must accept the RGBA vector alternative with alpha in the fourth component.");

        JsonElement vectorReencoded = ParticleTypes.CODEC.EncodeStart(ops, vectorDecoded).GetOrThrow();
        Require(ops.Get(vectorReencoded, "color").GetOrThrow().GetInt32() == expectedColor,
            "Color particle encoding must normalize alternative vectors back to the primary integer representation.");

        ColorParticleOption opaque = ColorParticleOption.Create(
            ParticleTypes.TINTED_LEAVES, 1.0F, 0.5F, 0.25F);
        Require(opaque.GetAlpha() == 1.0F &&
                ReferenceEquals(opaque.GetParticleType(), ParticleTypes.TINTED_LEAVES),
            "The RGB factory overload must produce an opaque color option for the supplied type.");

        Require(!ParticleTypes.ENTITY_EFFECT.OverrideLimiter &&
                !ParticleTypes.TINTED_LEAVES.OverrideLimiter &&
                !ParticleTypes.FLASH.OverrideLimiter,
            "The three color particle types must retain false overrideLimiter flags.");

        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;
        Require(registry.GetId(ParticleTypes.END_ROD) < registry.GetId(ParticleTypes.ENTITY_EFFECT) &&
                registry.GetId(ParticleTypes.ENTITY_EFFECT) < registry.GetId(ParticleTypes.EXPLOSION) &&
                registry.GetId(ParticleTypes.YELLOW_POPLAR_LEAVES) < registry.GetId(ParticleTypes.TINTED_LEAVES) &&
                registry.GetId(ParticleTypes.TINTED_LEAVES) < registry.GetId(ParticleTypes.SCULK_SOUL) &&
                registry.GetId(ParticleTypes.SOUL) < registry.GetId(ParticleTypes.FLASH) &&
                registry.GetId(ParticleTypes.FLASH) < registry.GetId(ParticleTypes.HAPPY_VILLAGER),
            "Color particle registrations must occupy their relative source declaration positions.");
    }


    private static void CheckByNameCodecFailures()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<ParticleType> codec = BuiltInRegistries.ParticleType.ByNameCodec();

        Require(codec.Parse(ops, ops.CreateString("asteria:ash")).GetOrThrow() ==
                ParticleTypes.ASH,
            "A registry by-name codec must resolve valid identifiers.");
        Require(codec.Parse(ops, ops.CreateString("asteria:missing_particle")).IsError,
            "A registry by-name codec must reject unknown identifiers.");
        Require(codec.Parse(ops, ops.CreateString("invalid namespace:bad")).IsError,
            "A registry by-name codec must reject invalid resource identifiers.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
