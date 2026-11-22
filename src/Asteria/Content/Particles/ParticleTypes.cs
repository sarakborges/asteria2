using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>
/// Simple built-in particle types from Minecraft Java 26.3. Field declaration order and
/// override-limiter flags follow the source; types with specialized payloads are added
/// together with their actual option classes and codecs.
/// </summary>
public static class ParticleTypes
{
    public static readonly ParticleType<BlockParticleOption> BLOCK =
        RegisterConfigured<BlockParticleOption>(
            "block", false, BlockParticleOption.CreateCodec);
    public static readonly ParticleType<BlockParticleOption> BLOCK_MARKER =
        RegisterConfigured<BlockParticleOption>(
            "block_marker", true, BlockParticleOption.CreateCodec);
    public static readonly SimpleParticleType BUBBLE = Register("bubble", false);
    public static readonly SimpleParticleType CLOUD = Register("cloud", false);
    public static readonly SimpleParticleType CRIT = Register("crit", false);
    public static readonly SimpleParticleType DAMAGE_INDICATOR = Register("damage_indicator", true);
    public static readonly SimpleParticleType DRIPPING_LAVA = Register("dripping_lava", false);
    public static readonly SimpleParticleType FALLING_LAVA = Register("falling_lava", false);
    public static readonly SimpleParticleType LANDING_LAVA = Register("landing_lava", false);
    public static readonly SimpleParticleType DRIPPING_WATER = Register("dripping_water", false);
    public static readonly SimpleParticleType FALLING_WATER = Register("falling_water", false);
    public static readonly ParticleType<DustParticleOptions> DUST =
        RegisterConfigured<DustParticleOptions>(
            "dust", false, static _ => DustParticleOptions.CODEC);
    public static readonly ParticleType<DustColorTransitionOptions> DUST_COLOR_TRANSITION =
        RegisterConfigured<DustColorTransitionOptions>(
            "dust_color_transition", false, static _ => DustColorTransitionOptions.CODEC);
    public static readonly ParticleType<SpellParticleOption> EFFECT =
        RegisterConfigured<SpellParticleOption>("effect", false, SpellParticleOption.CreateCodec);
    public static readonly SimpleParticleType ENCHANTED_HIT = Register("enchanted_hit", false);
    public static readonly SimpleParticleType ENCHANT = Register("enchant", false);
    public static readonly ParticleType<ColorParticleOption> ENTITY_EFFECT =
        RegisterConfigured<ColorParticleOption>("entity_effect", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType EXPLOSION_EMITTER = Register("explosion_emitter", true);
    public static readonly SimpleParticleType EXPLOSION = Register("explosion", true);
    public static readonly ParticleType<BlockParticleOption> FALLING_DUST =
        RegisterConfigured<BlockParticleOption>(
            "falling_dust", false, BlockParticleOption.CreateCodec);
    public static readonly SimpleParticleType FLAME = Register("flame", false);
    public static readonly ParticleType<ColorParticleOption> TINTED_LEAVES =
        RegisterConfigured<ColorParticleOption>("tinted_leaves", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType SOUL = Register("soul", false);
    public static readonly ParticleType<ColorParticleOption> FLASH =
        RegisterConfigured<ColorParticleOption>("flash", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType HEART = Register("heart", false);
    public static readonly ParticleType<SpellParticleOption> INSTANT_EFFECT =
        RegisterConfigured<SpellParticleOption>("instant_effect", false, SpellParticleOption.CreateCodec);
    public static readonly ParticleType<ItemParticleOption> ITEM =
        RegisterConfigured<ItemParticleOption>(
            "item", false, ItemParticleOption.CreateCodec);
    public static readonly ParticleType<VibrationParticleOption> VIBRATION =
        RegisterConfigured<VibrationParticleOption>(
            "vibration", false, static _ => VibrationParticleOption.CODEC);
    public static readonly ParticleType<TrailParticleOption> TRAIL =
        RegisterConfigured<TrailParticleOption>(
            "trail", false, static _ => TrailParticleOption.CODEC);
    public static readonly SimpleParticleType LARGE_SMOKE = Register("large_smoke", false);
    public static readonly SimpleParticleType LAVA = Register("lava", false);
    public static readonly SimpleParticleType POOF = Register("poof", true);
    public static readonly SimpleParticleType PORTAL = Register("portal", false);
    public static readonly SimpleParticleType RAIN = Register("rain", false);
    public static readonly SimpleParticleType SMOKE = Register("smoke", false);
    public static readonly SimpleParticleType WHITE_SMOKE = Register("white_smoke", false);
    public static readonly SimpleParticleType SWEEP_ATTACK = Register("sweep_attack", true);
    public static readonly SimpleParticleType UNDERWATER = Register("underwater", false);
    public static readonly SimpleParticleType SPLASH = Register("splash", false);
    public static readonly SimpleParticleType BUBBLE_POP = Register("bubble_pop", false);
    public static readonly SimpleParticleType ASH = Register("ash", false);
    public static readonly SimpleParticleType REVERSE_PORTAL = Register("reverse_portal", false);
    public static readonly SimpleParticleType WHITE_ASH = Register("white_ash", false);
    public static readonly SimpleParticleType SMALL_FLAME = Register("small_flame", false);
    public static readonly SimpleParticleType SNOWFLAKE = Register("snowflake", false);
    public static readonly SimpleParticleType GLOW = Register("glow", true);

    public const int SimpleTypeCount = 33;
    public const int ColorTypeCount = 3;
    public const int SpellOptionCount = 2;
    public const int NumericOptionCount = 0;
    public const int RecordOptionCount = 1;
    public const int ScalableOptionCount = 2;
    public const int BlockOptionCount = 3;
    public const int ItemOptionCount = 1;
    public const int VibrationOptionCount = 1;
    public const int RegisteredTypeCount =
        SimpleTypeCount + ColorTypeCount + SpellOptionCount + NumericOptionCount +
        RecordOptionCount + ScalableOptionCount + BlockOptionCount + ItemOptionCount + VibrationOptionCount;

    public static Codec<IParticleOptions> CODEC { get; } =
        MapCodec<IParticleOptions>.Dispatch(
            BuiltInRegistries.ParticleType.ByNameCodec().FieldOf("type"),
            static options => DataResult<ParticleType>.Success(options.GetParticleType()),
            static type => DataResult<MapCodec<IParticleOptions>>.Success(type.OptionsCodec))
        .Codec();

    private static ParticleType<TOptions> RegisterConfigured<TOptions>(
        string name,
        bool overrideLimiter,
        Func<ParticleType<TOptions>, MapCodec<TOptions>> codecFactory)
        where TOptions : class, IParticleOptions
    {
        var particleType = new ConfiguredParticleType<TOptions>(overrideLimiter, codecFactory);
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(name);
        RegistryKey<ParticleType> key =
            RegistryKey<ParticleType>.Create(BuiltInRegistries.ParticleType.Key, location);
        BuiltInRegistries.ParticleType.Register(key, particleType, RegistrationInfo.BuiltIn);
        return particleType;
    }

    private static SimpleParticleType Register(string name, bool overrideLimiter)
    {
        var particleType = new SimpleParticleType(overrideLimiter);
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(name);
        RegistryKey<ParticleType> key =
            RegistryKey<ParticleType>.Create(BuiltInRegistries.ParticleType.Key, location);
        BuiltInRegistries.ParticleType.Register(key, particleType, RegistrationInfo.BuiltIn);
        return particleType;
    }

    internal static void RegisterAll()
    {
        // Accessing the first field triggers the ordered static registrations above.
        _ = BLOCK;
    }
}
