using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.Attributes;

/// <summary>Built-in environment definitions whose value types are currently implemented.</summary>
public static class EnvironmentAttributes
{
    public static Codec<IEnvironmentAttribute> CODEC =>
        BuiltInRegistries.EnvironmentAttribute.ByNameCodec();

    public static readonly EnvironmentAttribute<float> FOG_START_DISTANCE = Float(0.0F, true, syncable: true);
    public static readonly EnvironmentAttribute<float> FOG_END_DISTANCE = Float(1024.0F, true, AttributeRange<float>.NonNegativeFloat, true);
    public static readonly EnvironmentAttribute<float> SKY_FOG_END_DISTANCE = Float(512.0F, true, AttributeRange<float>.NonNegativeFloat, true);
    public static readonly EnvironmentAttribute<float> CLOUD_FOG_END_DISTANCE = Float(2048.0F, true, AttributeRange<float>.NonNegativeFloat, true);
    public static readonly EnvironmentAttribute<float> WATER_FOG_START_DISTANCE = Float(-8.0F, true, syncable: true);
    public static readonly EnvironmentAttribute<float> WATER_FOG_END_DISTANCE = Float(96.0F, true, AttributeRange<float>.NonNegativeFloat, true);
    public static readonly EnvironmentAttribute<float> CLOUD_HEIGHT = Float(192.33F, true, syncable: true);
    public static readonly EnvironmentAttribute<float> SUN_ANGLE = Angle();
    public static readonly EnvironmentAttribute<float> MOON_ANGLE = Angle();
    public static readonly EnvironmentAttribute<float> STAR_ANGLE = Angle();
    public static readonly EnvironmentAttribute<float> STAR_BRIGHTNESS = Float(0.0F, true, AttributeRange<float>.UnitFloat, true);
    public static readonly EnvironmentAttribute<float> SKY_LIGHT_FACTOR = Float(1.0F, true, AttributeRange<float>.UnitFloat, true);
    public static readonly EnvironmentAttribute<float> SKY_LIGHT_LEVEL = Float(15.0F, false, AttributeRange<float>.OfFloat(0.0F, 15.0F), true, false);
    public static readonly EnvironmentAttribute<bool> WATER_EVAPORATES = Boolean(false, true);
    public static readonly EnvironmentAttribute<bool> FAST_LAVA = Boolean(false, true, false);
    public static readonly EnvironmentAttribute<bool> INCREASED_FIRE_BURNOUT = Boolean(false);

    public static readonly EnvironmentAttribute<IReadOnlyList<AmbientParticle>> AMBIENT_PARTICLES =
        EnvironmentAttribute<IReadOnlyList<AmbientParticle>>.CreateBuilder(AttributeTypes.AMBIENT_PARTICLES)
            .DefaultValue(Array.Empty<AmbientParticle>())
            .Syncable()
            .Build();

    internal static void RegisterAll()
    {
        Register("visual/fog_start_distance", FOG_START_DISTANCE);
        Register("visual/fog_end_distance", FOG_END_DISTANCE);
        Register("visual/sky_fog_end_distance", SKY_FOG_END_DISTANCE);
        Register("visual/cloud_fog_end_distance", CLOUD_FOG_END_DISTANCE);
        Register("visual/water_fog_start_distance", WATER_FOG_START_DISTANCE);
        Register("visual/water_fog_end_distance", WATER_FOG_END_DISTANCE);
        Register("visual/cloud_height", CLOUD_HEIGHT);
        Register("visual/sun_angle", SUN_ANGLE);
        Register("visual/moon_angle", MOON_ANGLE);
        Register("visual/star_angle", STAR_ANGLE);
        Register("visual/star_brightness", STAR_BRIGHTNESS);
        Register("visual/sky_light_factor", SKY_LIGHT_FACTOR);
        Register("gameplay/sky_light_level", SKY_LIGHT_LEVEL);
        Register("gameplay/water_evaporates", WATER_EVAPORATES);
        Register("gameplay/fast_lava", FAST_LAVA);
        Register("gameplay/increased_fire_burnout", INCREASED_FIRE_BURNOUT);
        Register("visual/ambient_particles", AMBIENT_PARTICLES);
    }

    private static EnvironmentAttribute<float> Float(
        float value, bool spatial, AttributeRange<float>? range = null, bool syncable = false, bool positional = true)
    {
        var builder = EnvironmentAttribute<float>.CreateBuilder(AttributeTypes.FLOAT).DefaultValue(value);
        if (range is not null) builder.ValueRange(range);
        if (spatial) builder.SpatiallyInterpolated();
        if (syncable) builder.Syncable();
        if (!positional) builder.NotPositional();
        return builder.Build();
    }

    private static EnvironmentAttribute<float> Angle() =>
        EnvironmentAttribute<float>.CreateBuilder(AttributeTypes.ANGLE_DEGREES)
            .DefaultValue(0.0F).SpatiallyInterpolated().Syncable().Build();

    private static EnvironmentAttribute<bool> Boolean(bool value, bool syncable = false, bool positional = true)
    {
        var builder = EnvironmentAttribute<bool>.CreateBuilder(AttributeTypes.BOOLEAN).DefaultValue(value);
        if (syncable) builder.Syncable();
        if (!positional) builder.NotPositional();
        return builder.Build();
    }

    private static void Register<T>(string path, EnvironmentAttribute<T> attribute)
    {
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(path);
        RegistryKey<IEnvironmentAttribute> key =
            RegistryKey<IEnvironmentAttribute>.Create(BuiltInRegistries.EnvironmentAttribute.Key, location);
        BuiltInRegistries.EnvironmentAttribute.Register(key, attribute, RegistrationInfo.BuiltIn);
    }
}
