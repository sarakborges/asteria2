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
    public static readonly SimpleParticleType ANGRY_VILLAGER = Register("angry_villager", false);
    public static readonly SimpleParticleType BUBBLE = Register("bubble", false);
    public static readonly SimpleParticleType SULFUR_BUBBLES = Register("sulfur_bubbles", false);
    public static readonly SimpleParticleType NOXIOUS_GAS = Register("noxious_gas", false);
    public static readonly SimpleParticleType NOXIOUS_GAS_CLOUD = Register("noxious_gas_cloud", false);
    public static readonly SimpleParticleType CLOUD = Register("cloud", false);
    public static readonly SimpleParticleType COPPER_FIRE_FLAME = Register("copper_fire_flame", false);
    public static readonly SimpleParticleType CRIT = Register("crit", false);
    public static readonly SimpleParticleType DAMAGE_INDICATOR = Register("damage_indicator", true);
    public static readonly ParticleType<PowerParticleOption> DRAGON_BREATH =
        RegisterConfigured<PowerParticleOption>("dragon_breath", false, PowerParticleOption.CreateCodec);
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
    public static readonly SimpleParticleType ELDER_GUARDIAN = Register("elder_guardian", true);
    public static readonly SimpleParticleType ENCHANTED_HIT = Register("enchanted_hit", false);
    public static readonly SimpleParticleType ENCHANT = Register("enchant", false);
    public static readonly SimpleParticleType END_ROD = Register("end_rod", false);
    public static readonly ParticleType<ColorParticleOption> ENTITY_EFFECT =
        RegisterConfigured<ColorParticleOption>("entity_effect", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType EXPLOSION_EMITTER = Register("explosion_emitter", true);
    public static readonly SimpleParticleType EXPLOSION = Register("explosion", true);
    public static readonly SimpleParticleType GUST = Register("gust", true);
    public static readonly SimpleParticleType SMALL_GUST = Register("small_gust", false);
    public static readonly SimpleParticleType GUST_EMITTER_LARGE = Register("gust_emitter_large", true);
    public static readonly SimpleParticleType GUST_EMITTER_SMALL = Register("gust_emitter_small", true);
    public static readonly SimpleParticleType SONIC_BOOM = Register("sonic_boom", true);
    public static readonly SimpleParticleType FIREWORK = Register("firework", false);
    public static readonly SimpleParticleType FISHING = Register("fishing", false);
    public static readonly SimpleParticleType FLAME = Register("flame", false);
    public static readonly SimpleParticleType INFESTED = Register("infested", false);
    public static readonly SimpleParticleType CHERRY_LEAVES = Register("cherry_leaves", false);
    public static readonly SimpleParticleType PALE_OAK_LEAVES = Register("pale_oak_leaves", false);
    public static readonly SimpleParticleType RED_POPLAR_LEAVES = Register("red_poplar_leaves", false);
    public static readonly SimpleParticleType ORANGE_POPLAR_LEAVES = Register("orange_poplar_leaves", false);
    public static readonly SimpleParticleType YELLOW_POPLAR_LEAVES = Register("yellow_poplar_leaves", false);
    public static readonly ParticleType<ColorParticleOption> TINTED_LEAVES =
        RegisterConfigured<ColorParticleOption>("tinted_leaves", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType SCULK_SOUL = Register("sculk_soul", false);
    public static readonly ParticleType<SculkChargeParticleOptions> SCULK_CHARGE =
        RegisterConfigured<SculkChargeParticleOptions>(
            "sculk_charge", true, static _ => SculkChargeParticleOptions.CODEC);
    public static readonly SimpleParticleType SCULK_CHARGE_POP = Register("sculk_charge_pop", true);
    public static readonly SimpleParticleType SOUL_FIRE_FLAME = Register("soul_fire_flame", false);
    public static readonly SimpleParticleType SOUL = Register("soul", false);
    public static readonly ParticleType<ColorParticleOption> FLASH =
        RegisterConfigured<ColorParticleOption>("flash", false, ColorParticleOption.CreateCodec);
    public static readonly SimpleParticleType HAPPY_VILLAGER = Register("happy_villager", false);
    public static readonly SimpleParticleType COMPOSTER = Register("composter", false);
    public static readonly SimpleParticleType HEART = Register("heart", false);
    public static readonly ParticleType<SpellParticleOption> INSTANT_EFFECT =
        RegisterConfigured<SpellParticleOption>("instant_effect", false, SpellParticleOption.CreateCodec);
    public static readonly ParticleType<TrailParticleOption> TRAIL =
        RegisterConfigured<TrailParticleOption>(
            "trail", false, static _ => TrailParticleOption.CODEC);
    public static readonly SimpleParticleType PAUSE_MOB_GROWTH = Register("pause_mob_growth", false);
    public static readonly SimpleParticleType RESET_MOB_GROWTH = Register("reset_mob_growth", false);
    public static readonly SimpleParticleType ITEM_SLIME = Register("item_slime", false);
    public static readonly SimpleParticleType ITEM_COBWEB = Register("item_cobweb", false);
    public static readonly SimpleParticleType ITEM_SNOWBALL = Register("item_snowball", false);
    public static readonly SimpleParticleType LARGE_SMOKE = Register("large_smoke", false);
    public static readonly SimpleParticleType LAVA = Register("lava", false);
    public static readonly SimpleParticleType MYCELIUM = Register("mycelium", false);
    public static readonly SimpleParticleType NOTE = Register("note", false);
    public static readonly SimpleParticleType POOF = Register("poof", true);
    public static readonly SimpleParticleType PORTAL = Register("portal", false);
    public static readonly SimpleParticleType RAIN = Register("rain", false);
    public static readonly SimpleParticleType SMOKE = Register("smoke", false);
    public static readonly SimpleParticleType WHITE_SMOKE = Register("white_smoke", false);
    public static readonly SimpleParticleType SNEEZE = Register("sneeze", false);
    public static readonly SimpleParticleType SPIT = Register("spit", true);
    public static readonly SimpleParticleType SQUID_INK = Register("squid_ink", true);
    public static readonly SimpleParticleType SWEEP_ATTACK = Register("sweep_attack", true);
    public static readonly SimpleParticleType TOTEM_OF_UNDYING = Register("totem_of_undying", false);
    public static readonly SimpleParticleType UNDERWATER = Register("underwater", false);
    public static readonly SimpleParticleType SPLASH = Register("splash", false);
    public static readonly SimpleParticleType WITCH = Register("witch", false);
    public static readonly SimpleParticleType BUBBLE_POP = Register("bubble_pop", false);
    public static readonly SimpleParticleType CURRENT_DOWN = Register("current_down", false);
    public static readonly SimpleParticleType BUBBLE_COLUMN_UP = Register("bubble_column_up", false);
    public static readonly SimpleParticleType NAUTILUS = Register("nautilus", false);
    public static readonly SimpleParticleType DOLPHIN = Register("dolphin", false);
    public static readonly SimpleParticleType CAMPFIRE_COSY_SMOKE = Register("campfire_cosy_smoke", true);
    public static readonly SimpleParticleType CAMPFIRE_SIGNAL_SMOKE = Register("campfire_signal_smoke", true);
    public static readonly SimpleParticleType DRIPPING_HONEY = Register("dripping_honey", false);
    public static readonly SimpleParticleType FALLING_HONEY = Register("falling_honey", false);
    public static readonly SimpleParticleType LANDING_HONEY = Register("landing_honey", false);
    public static readonly SimpleParticleType FALLING_NECTAR = Register("falling_nectar", false);
    public static readonly SimpleParticleType FALLING_SPORE_BLOSSOM = Register("falling_spore_blossom", false);
    public static readonly SimpleParticleType ASH = Register("ash", false);
    public static readonly SimpleParticleType CRIMSON_SPORE = Register("crimson_spore", false);
    public static readonly SimpleParticleType WARPED_SPORE = Register("warped_spore", false);
    public static readonly SimpleParticleType SPORE_BLOSSOM_AIR = Register("spore_blossom_air", false);
    public static readonly SimpleParticleType DRIPPING_OBSIDIAN_TEAR = Register("dripping_obsidian_tear", false);
    public static readonly SimpleParticleType FALLING_OBSIDIAN_TEAR = Register("falling_obsidian_tear", false);
    public static readonly SimpleParticleType LANDING_OBSIDIAN_TEAR = Register("landing_obsidian_tear", false);
    public static readonly SimpleParticleType REVERSE_PORTAL = Register("reverse_portal", false);
    public static readonly SimpleParticleType WHITE_ASH = Register("white_ash", false);
    public static readonly SimpleParticleType SMALL_FLAME = Register("small_flame", false);
    public static readonly SimpleParticleType SNOWFLAKE = Register("snowflake", false);
    public static readonly SimpleParticleType DRIPPING_DRIPSTONE_LAVA = Register("dripping_dripstone_lava", false);
    public static readonly SimpleParticleType FALLING_DRIPSTONE_LAVA = Register("falling_dripstone_lava", false);
    public static readonly SimpleParticleType DRIPPING_DRIPSTONE_WATER = Register("dripping_dripstone_water", false);
    public static readonly SimpleParticleType FALLING_DRIPSTONE_WATER = Register("falling_dripstone_water", false);
    public static readonly SimpleParticleType GLOW_SQUID_INK = Register("glow_squid_ink", true);
    public static readonly SimpleParticleType GLOW = Register("glow", true);
    public static readonly SimpleParticleType WAX_ON = Register("wax_on", true);
    public static readonly SimpleParticleType WAX_OFF = Register("wax_off", true);
    public static readonly SimpleParticleType ELECTRIC_SPARK = Register("electric_spark", true);
    public static readonly SimpleParticleType SCRAPE = Register("scrape", true);
    public static readonly ParticleType<ShriekParticleOption> SHRIEK =
        RegisterConfigured<ShriekParticleOption>(
            "shriek", false, static _ => ShriekParticleOption.CODEC);
    public static readonly SimpleParticleType EGG_CRACK = Register("egg_crack", false);
    public static readonly SimpleParticleType DUST_PLUME = Register("dust_plume", false);
    public static readonly SimpleParticleType TRIAL_SPAWNER_DETECTED_PLAYER = Register("trial_spawner_detection", true);
    public static readonly SimpleParticleType TRIAL_SPAWNER_DETECTED_PLAYER_OMINOUS = Register("trial_spawner_detection_ominous", true);
    public static readonly SimpleParticleType VAULT_CONNECTION = Register("vault_connection", true);
    public static readonly SimpleParticleType OMINOUS_SPAWNING = Register("ominous_spawning", true);
    public static readonly SimpleParticleType RAID_OMEN = Register("raid_omen", false);
    public static readonly SimpleParticleType TRIAL_OMEN = Register("trial_omen", false);
    public static readonly SimpleParticleType FIREFLY = Register("firefly", false);
    public static readonly SimpleParticleType SULFUR_CUBE_GOO = Register("sulfur_cube_goo", false);

    public const int SimpleTypeCount = 106;
    public const int ColorTypeCount = 3;
    public const int SpellOptionCount = 2;
    public const int NumericOptionCount = 3;
    public const int RecordOptionCount = 1;
    public const int ScalableOptionCount = 2;
    public const int RegisteredTypeCount =
        SimpleTypeCount + ColorTypeCount + SpellOptionCount + NumericOptionCount +
        RecordOptionCount + ScalableOptionCount;

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
        _ = ANGRY_VILLAGER;
    }
}
