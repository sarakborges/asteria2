using System.Text.Json;
using Asteria.Content.Identifiers;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class ParticleChecks
{
    public static void Run()
    {
        CheckRegistry();
        CheckSimpleOptionsCodec();
        CheckByNameCodecFailures();
    }

    private static void CheckRegistry()
    {
        Registry<ParticleType> registry = BuiltInRegistries.ParticleType;

        Require(registry.IsFrozen,
            "The particle type registry must be frozen after built-in bootstrap.");
        Require(registry.Count == ParticleTypes.SimpleTypeCount,
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
