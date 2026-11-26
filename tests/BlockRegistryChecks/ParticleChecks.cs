using System.Numerics;
using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Core;
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
        CheckTrailParticleOptionCodec();
        DustParticleChecks.Run();
        BlockParticleChecks.Run();
        ItemParticleChecks.Run();
        VibrationParticleChecks.Run();
        CheckByNameCodecFailures();
    }

    private static void CheckRegistry()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;

        Require(registry.IsFrozen,
            "The particle type registry must be frozen after built-in bootstrap.");
        Require(ParticleTypes.RegisteredTypeCount == 46 &&
                ParticleTypes.SimpleTypeCount == 33 &&
                ParticleTypes.ColorTypeCount == 3 &&
                ParticleTypes.SpellOptionCount == 2 &&
                ParticleTypes.NumericOptionCount == 0,
            "The in-scope particle registry must contain 46 reusable types and no object-specific payload types.");
        Require(registry.Count == ParticleTypes.RegisteredTypeCount,
            "Every retained particle type must be registered.");

        Require(ReferenceEquals(
                registry.GetValue(ResourceLocation.WithDefaultNamespace("bubble")),
                ParticleTypes.BUBBLE),
            "The retained generic bubble particle must resolve by its Minecraft identifier.");
        Require(registry.GetId(ParticleTypes.BLOCK) < registry.GetId(ParticleTypes.BLOCK_MARKER) &&
                registry.GetId(ParticleTypes.BLOCK_MARKER) < registry.GetId(ParticleTypes.BUBBLE),
            "Generic block particle registrations must preserve their relative source order.");
        Require(registry.GetId(ParticleTypes.FALLING_WATER) < registry.GetId(ParticleTypes.DUST) &&
                registry.GetId(ParticleTypes.DUST) < registry.GetId(ParticleTypes.DUST_COLOR_TRANSITION) &&
                registry.GetId(ParticleTypes.DUST_COLOR_TRANSITION) < registry.GetId(ParticleTypes.EFFECT),
            "Generic fluid, dust and spell particle registrations must preserve their relative source order.");
        Require(registry.GetId(ParticleTypes.INSTANT_EFFECT) < registry.GetId(ParticleTypes.ITEM) &&
                registry.GetId(ParticleTypes.ITEM) < registry.GetId(ParticleTypes.VIBRATION) &&
                registry.GetId(ParticleTypes.VIBRATION) < registry.GetId(ParticleTypes.TRAIL) &&
                registry.GetId(ParticleTypes.TRAIL) < registry.GetId(ParticleTypes.LARGE_SMOKE),
            "Generic item, vibration and trail particle registrations must preserve their relative source order.");

        foreach (string excluded in new[]
        {
            "angry_villager", "dragon_breath", "sculk_charge", "shriek",
            "pause_mob_growth", "reset_mob_growth",
            "trial_spawner_detection", "vault_connection", "raid_omen",
            "firefly", "totem_of_undying", "campfire_cosy_smoke",
            "falling_dripstone_water", "sulfur_bubbles", "noxious_gas",
            "copper_fire_flame", "sulfur_cube_goo", "fishing", "soul_fire_flame",
            "current_down", "bubble_column_up", "geyser", "geyser_base", "geyser_poof",
            "geyser_plume", "dust_pillar", "block_crumble"
        })
        {
            Require(registry.GetValue(ResourceLocation.WithDefaultNamespace(excluded)) is null,
                $"Object-specific particle '{excluded}' must not be registered in the generic catalog.");
        }

        Require(ParticleTypes.DAMAGE_INDICATOR.OverrideLimiter &&
                ParticleTypes.EXPLOSION.OverrideLimiter &&
                ParticleTypes.POOF.OverrideLimiter &&
                ParticleTypes.GLOW.OverrideLimiter,
            "Retained particles marked overrideLimiter=true must preserve that flag.");
        Require(!ParticleTypes.BUBBLE.OverrideLimiter &&
                !ParticleTypes.ASH.OverrideLimiter &&
                !ParticleTypes.BLOCK.OverrideLimiter &&
                !ParticleTypes.FALLING_DUST.OverrideLimiter &&
                !ParticleTypes.ITEM.OverrideLimiter &&
                !ParticleTypes.VIBRATION.OverrideLimiter &&
                ParticleTypes.BLOCK_MARKER.OverrideLimiter,
            "Generic block, item and vibration particle types must retain their individual limiter flags.");
    }

    private static void CheckSimpleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        IParticleOptions source = ParticleTypes.ASH;

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(encoded.ValueKind == JsonValueKind.Object &&
                ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:ash",
            "The particle options codec must encode the retained simple type key.");

        IParticleOptions decoded = ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded, ParticleTypes.ASH) &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.ASH),
            "A simple particle options codec must decode to its registered singleton instance.");
    }

    private static void CheckColorParticleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        int packedColor = unchecked((int)0x80402010u);
        ColorParticleOption source = ColorParticleOption.Create(ParticleTypes.ENTITY_EFFECT, packedColor);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:entity_effect" &&
                ops.Get(encoded, "color").GetOrThrow().GetInt32() == packedColor,
            "Color particle options must serialize their reusable discriminator and packed ARGB integer.");

        ColorParticleOption decoded =
            (ColorParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();
        Require(ReferenceEquals(decoded.Type, ParticleTypes.ENTITY_EFFECT) &&
                decoded.Color == packedColor &&
                Math.Abs(decoded.GetRed() - (64.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetGreen() - (32.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetBlue() - (16.0F / 255.0F)) < 0.000001F &&
                Math.Abs(decoded.GetAlpha() - (128.0F / 255.0F)) < 0.000001F,
            "Color particle options must round-trip ARGB and expose normalized channel accessors.");

        JsonElement vectorOption = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("minecraft:flash")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("color"),
                ops.CreateList(new[]
                {
                    ops.CreateFloat(1.0F), ops.CreateFloat(0.5F),
                    ops.CreateFloat(0.25F), ops.CreateFloat(0.75F)
                }))
        });
        ColorParticleOption vectorDecoded =
            (ColorParticleOption)ParticleTypes.CODEC.Parse(ops, vectorOption).GetOrThrow();
        int expectedColor = Argb.Color(191, 255, 127, 63);
        Require(vectorDecoded.Color == expectedColor &&
                ReferenceEquals(vectorDecoded.Type, ParticleTypes.FLASH),
            "Color particle codecs must accept RGBA vector alternatives.");

        ColorParticleOption opaque = ColorParticleOption.Create(
            ParticleTypes.TINTED_LEAVES, 1.0F, 0.5F, 0.25F);
        Require(opaque.GetAlpha() == 1.0F &&
                ReferenceEquals(opaque.GetParticleType(), ParticleTypes.TINTED_LEAVES),
            "The RGB factory overload must produce an opaque option for the supplied generic type.");
    }

    private static void CheckSpellParticleOptionsCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        SpellParticleOption defaultOption = SpellParticleOption.Create(ParticleTypes.EFFECT, -1, 1.0F);
        JsonElement defaultEncoded = ParticleTypes.CODEC.EncodeStart(ops, defaultOption).GetOrThrow();

        Require(ops.Get(defaultEncoded, "type").GetOrThrow().GetString() == "minecraft:effect" &&
                !ops.Get(defaultEncoded, "color").TryGetResult(out _) &&
                !ops.Get(defaultEncoded, "power").TryGetResult(out _),
            "Spell particle codecs must omit fields equal to their defaults.");

        SpellParticleOption defaultDecoded =
            (SpellParticleOption)ParticleTypes.CODEC.Parse(ops, defaultEncoded).GetOrThrow();
        Require(ReferenceEquals(defaultDecoded.Type, ParticleTypes.EFFECT) &&
                defaultDecoded.Color == -1 && defaultDecoded.Power == 1.0F,
            "Missing spell particle fields must decode to their generic defaults.");

        int rgbColor = Argb.Color(255, 128, 64, 32);
        SpellParticleOption source = SpellParticleOption.Create(ParticleTypes.EFFECT, rgbColor, 2.5F);
        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        SpellParticleOption decoded =
            (SpellParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();

        Require(ops.Get(encoded, "color").GetOrThrow().GetInt32() == rgbColor &&
                ops.Get(encoded, "power").GetOrThrow().GetSingle() == 2.5F &&
                ReferenceEquals(decoded.Type, ParticleTypes.EFFECT) &&
                decoded.Color == rgbColor && decoded.Power == 2.5F,
            "Spell particle options must round-trip their reusable color and power payload.");

        JsonElement invalidPower = ops.CreateMap(new[]
        {
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("type"), ops.CreateString("minecraft:effect")),
            new KeyValuePair<JsonElement, JsonElement>(
                ops.CreateString("power"), ops.CreateString("not-a-number"))
        });
        Require(ParticleTypes.CODEC.Parse(ops, invalidPower).IsError,
            "A present invalid spell particle power must fail instead of falling back to its default.");
    }

    private static void CheckTrailParticleOptionCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        var target = new Vec3(1.25, -2.5, 3.75);
        int rgb = Argb.Color(255, 128, 64, 32);
        var source = new TrailParticleOption(target, rgb, 24);

        JsonElement encoded = ParticleTypes.CODEC.EncodeStart(ops, source).GetOrThrow();
        TrailParticleOption decoded =
            (TrailParticleOption)ParticleTypes.CODEC.Parse(ops, encoded).GetOrThrow();

        Require(ops.Get(encoded, "type").GetOrThrow().GetString() == "minecraft:trail" &&
                ops.Get(encoded, "color").GetOrThrow().GetInt32() == rgb &&
                ops.Get(encoded, "duration").GetOrThrow().GetInt32() == 24 &&
                decoded == source &&
                ReferenceEquals(decoded.GetParticleType(), ParticleTypes.TRAIL),
            "Generic trail options must round-trip target, RGB color and positive duration.");

        foreach (int invalidDuration in new[] { 0, -1, int.MinValue })
        {
            JsonElement invalid = ops.CreateMap(new[]
            {
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("type"), ops.CreateString("minecraft:trail")),
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("target"),
                    ops.CreateList(new[]
                    {
                        ops.CreateDouble(1.0), ops.CreateDouble(2.0), ops.CreateDouble(3.0)
                    })),
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("color"), ops.CreateInt(rgb)),
                new KeyValuePair<JsonElement, JsonElement>(
                    ops.CreateString("duration"), ops.CreateInt(invalidDuration))
            });
            Require(ParticleTypes.CODEC.Parse(ops, invalid).IsError,
                "Trail particle codecs must reject non-positive duration values.");
        }
    }

    private static void CheckByNameCodecFailures()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Codec<ParticleType> codec = BuiltInRegistries.ParticleType.ByNameCodec();

        Require(codec.Parse(ops, ops.CreateString("minecraft:ash")).GetOrThrow() == ParticleTypes.ASH,
            "A registry by-name codec must resolve valid retained identifiers.");
        Require(codec.Parse(ops, ops.CreateString("minecraft:raid_omen")).IsError &&
                codec.Parse(ops, ops.CreateString("minecraft:missing_particle")).IsError,
            "A registry by-name codec must reject excluded and unknown identifiers.");
        Require(codec.Parse(ops, ops.CreateString("invalid namespace:bad")).IsError,
            "A registry by-name codec must reject invalid resource identifiers.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}