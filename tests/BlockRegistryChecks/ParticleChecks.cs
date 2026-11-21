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
        CheckSpellParticleOptionsCodec();
        CheckPowerParticleOptionCodec();
        CheckSculkChargeParticleOptionsCodec();
        CheckShriekParticleOptionCodec();
        CheckByNameCodecFailures();
    }

    private static void CheckRegistry()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;

        Require(registry.IsFrozen,
            "The particle type registry must be frozen after built-in bootstrap.");
        Require(ParticleTypes.RegisteredTypeCount == 114,
            "The local particle registry must include 106 simple, three color, two spell and three numeric option types.");
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
        Require(registry.GetId(ParticleTypes.DAMAGE_INDICATOR) < registry.GetId(ParticleTypes.DRAGON_BREATH) &&
                registry.GetId(ParticleTypes.DRAGON_BREATH) < registry.GetId(ParticleTypes.DRIPPING_LAVA),
            "Dragon breath must occupy its source declaration position.");
        Require(registry.GetId(ParticleTypes.SCULK_SOUL) < registry.GetId(ParticleTypes.SCULK_CHARGE) &&
                registry.GetId(ParticleTypes.SCULK_CHARGE) < registry.GetId(ParticleTypes.SCULK_CHARGE_POP),
            "Sculk charge must occupy its source declaration position.");
        Require(registry.GetId(ParticleTypes.SCRAPE) < registry.GetId(ParticleTypes.SHRIEK) &&
                registry.GetId(ParticleTypes.SHRIEK) < registry.GetId(ParticleTypes.EGG_CRACK),
            "Shriek must occupy its source declaration position.");

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
        ColorParticleOption decoded = (ColorParticleOption)decodedOptions;
        Require(ReferenceEquals(decoded.Type, ParticleTypes.ENTITY_EFFECT) &&
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


    private static void CheckSpellParticleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        SpellParticleOption defaultOption =
            SpellParticleOption.Create(ParticleTypes.EFFECT, -1, 1.0F);
        JsonElement defaultEncoded =
            ParticleTypes.CODEC.EncodeStart(ops, defaultOption).GetOrThrow();
        Require(ops.Get(defaultEncoded, "type").GetOrThrow().GetString() == "asteria:effect" &&
                !ops.Get(defaultEncoded, "color").TryGetResult(out _) &&
                !ops.Get(defaultEncoded, "power").TryGetResult(out _),
            "Spell particle codec must omit both fields when they equal their defaults.");

        SpellParticleOption defaultDecoded =
            (SpellParticleOption)ParticleTypes.CODEC.Parse(ops, defaultEncoded).GetOrThrow();
        Require(ReferenceEquals(defaultDecoded.Type, ParticleTypes.EFFECT) &&
                defaultDecoded.Color == -1 &&
                defaultDecoded.Power == 1.0F,
            "Missing spell particle fields must decode to color -1 and power 1.0.");

        int rgbColor = Argb.Color(255, 128, 64, 32);
        SpellParticleOption source =
            SpellParticleOption.Create(ParticleTypes.EFFECT, rgbColor, 2.5F);
        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "color").GetOrThrow().GetInt32() == rgbColor &&
                ops.Get(encoded, "power").GetOrThrow().GetSingle() == 2.5F,
            "Non-default spell particle fields must encode as packed RGB and float power.");

        SpellParticleOption decoded =
            (SpellParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded.Type, ParticleTypes.EFFECT) &&
                decoded.Color == rgbColor &&
                decoded.Power == 2.5F &&
                Math.Abs(decoded.GetRed() - (128.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetGreen() - (64.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetBlue() - (32.0F / 255.0F)) < 0.000001F,
            "Spell particle options must round-trip color, power and normalized channel accessors.");

        JsonElement vectorOption = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"),
                ops.CreateString("asteria:instant_effect")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("color"),
                ops.CreateList(new[]
                {
                    ops.CreateFloat(0.5F),
                    ops.CreateFloat(0.25F),
                    ops.CreateFloat(0.75F)
                }))
        });
        SpellParticleOption vectorDecoded =
            (SpellParticleOption)ParticleTypes.CODEC.Parse(ops, vectorOption).GetOrThrow();
        int expectedVectorColor = Argb.Color(255, 127, 63, 191);
        Require(ReferenceEquals(vectorDecoded.Type, ParticleTypes.INSTANT_EFFECT) &&
                vectorDecoded.Color == expectedVectorColor &&
                vectorDecoded.Power == 1.0F,
            "Spell particle codec must accept the RGB vector alternative and default absent power.");

        JsonElement invalidColor = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"),
                ops.CreateString("asteria:effect")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("color"),
                ops.CreateString("not-a-color"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidColor).IsError,
            "A present invalid spell particle color must fail instead of falling back to its default.");

        JsonElement invalidPower = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"),
                ops.CreateString("asteria:effect")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("power"),
                ops.CreateString("not-a-number"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidPower).IsError,
            "A present invalid spell particle power must fail instead of falling back to its default.");

        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;
        Require(registry.GetId(ParticleTypes.FALLING_WATER) < registry.GetId(ParticleTypes.EFFECT) &&
                registry.GetId(ParticleTypes.EFFECT) < registry.GetId(ParticleTypes.ELDER_GUARDIAN) &&
                registry.GetId(ParticleTypes.HEART) < registry.GetId(ParticleTypes.INSTANT_EFFECT) &&
                registry.GetId(ParticleTypes.INSTANT_EFFECT) < registry.GetId(ParticleTypes.PAUSE_MOB_GROWTH),
            "Spell particle types must preserve their relative built-in declaration positions.");

        Require(!ParticleTypes.EFFECT.OverrideLimiter && !ParticleTypes.INSTANT_EFFECT.OverrideLimiter,
            "Both spell particle types must retain overrideLimiter=false.");
    }



    private static void CheckPowerParticleOptionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;

        PowerParticleOption defaultOption =
            PowerParticleOption.Create(ParticleTypes.DRAGON_BREATH, 1.0F);
        JsonElement defaultEncoded =
            ParticleTypes.CODEC.EncodeStart(ops, defaultOption).GetOrThrow();
        Require(ops.Get(defaultEncoded, "type").GetOrThrow().GetString() == "asteria:dragon_breath" &&
                !ops.Get(defaultEncoded, "power").TryGetResult(out _),
            "Dragon breath must omit its optional power field when it equals 1.0.");

        PowerParticleOption defaultDecoded =
            (PowerParticleOption)ParticleTypes.CODEC.Parse(ops, defaultEncoded).GetOrThrow();
        Require(ReferenceEquals(defaultDecoded.Type, ParticleTypes.DRAGON_BREATH) &&
                defaultDecoded.GetPower() == 1.0F,
            "Dragon breath must default absent power to 1.0.");

        PowerParticleOption source =
            PowerParticleOption.Create(ParticleTypes.DRAGON_BREATH, 0.375F);
        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "power").GetOrThrow().GetSingle() == 0.375F,
            "Dragon breath must encode non-default power as a float.");

        PowerParticleOption decoded =
            (PowerParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded.GetParticleType(), ParticleTypes.DRAGON_BREATH) &&
                decoded.GetPower() == 0.375F,
            "Dragon breath power options must round-trip their type and payload.");

        JsonElement invalid = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:dragon_breath")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("power"), ops.CreateString("invalid"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalid).IsError,
            "A present invalid dragon breath power must fail rather than use the default.");

        Require(!ParticleTypes.DRAGON_BREATH.OverrideLimiter,
            "Dragon breath must retain overrideLimiter=false.");
    }

    private static void CheckSculkChargeParticleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var source = new SculkChargeParticleOptions(1.25F);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:sculk_charge" &&
                ops.Get(encoded, "roll").GetOrThrow().GetSingle() == 1.25F,
            "Sculk charge must encode the required roll field and its particle type.");

        SculkChargeParticleOptions decoded =
            (SculkChargeParticleOptions)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded == source &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.SCULK_CHARGE),
            "Sculk charge options must round-trip roll and resolve the registered type.");

        JsonElement missingRoll = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:sculk_charge"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, missingRoll).IsError,
            "Sculk charge must reject an absent required roll field.");

        JsonElement invalidRoll = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:sculk_charge")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("roll"), ops.CreateString("invalid"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidRoll).IsError,
            "Sculk charge must reject an invalid roll value.");

        Require(ParticleTypes.SCULK_CHARGE.OverrideLimiter,
            "Sculk charge must retain overrideLimiter=true.");
    }


    private static void CheckShriekParticleOptionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var source = new ShriekParticleOption(37);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "asteria:shriek" &&
                ops.Get(encoded, "delay").GetOrThrow().GetInt32() == 37,
            "Shriek particle options must encode their discriminator and required integer delay.");

        ShriekParticleOption decoded =
            (ShriekParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(decoded.GetDelay() == 37 &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.SHRIEK),
            "Shriek particle options must round-trip delay and resolve the registered type.");

        var negativeDelay = new ShriekParticleOption(-17);
        JsonElement negativeEncoded = ParticleTypes.CODEC.EncodeStart(ops, negativeDelay).GetOrThrow();
        ShriekParticleOption negativeDecoded =
            (ShriekParticleOption)ParticleTypes.CODEC.Parse(ops, negativeEncoded).GetOrThrow();
        Require(negativeDecoded.GetDelay() == -17,
            "Shriek delay must preserve signed integer values without added range restrictions.");

        JsonElement missingDelay = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:shriek"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, missingDelay).IsError,
            "Shriek particle codec must reject a missing required delay field.");

        JsonElement invalidDelay = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("asteria:shriek")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("delay"), ops.CreateString("not_an_integer"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidDelay).IsError,
            "Shriek particle codec must reject a non-integer delay.");

        Require(!ParticleTypes.SHRIEK.OverrideLimiter,
            "Shriek particle type must retain overrideLimiter=false.");
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
