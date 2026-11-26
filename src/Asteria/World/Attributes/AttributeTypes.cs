using System.Numerics;
using Asteria.Content.Identifiers;
using Asteria.Content.Particles;
using Asteria.Content.Registries;
using Asteria.World.Attributes.Modifiers;
using Asteria.World.Level;
using Asteria.Serialization;

namespace Asteria.World.Attributes;

public static class AttributeTypes
{
    public static readonly AttributeType<bool> BOOLEAN =
        AttributeType<bool>.OfNotInterpolated(Codecs.BOOL, BooleanModifier.LIBRARY);

    public static readonly AttributeType<float> FLOAT =
        AttributeType<float>.OfInterpolated(
            Codecs.FLOAT,
            LerpFunction<float>.OfFloat(),
            FloatModifier.LIBRARY,
            toFloat: static value => value);

    public static readonly AttributeType<float> ANGLE_DEGREES =
        AttributeType<float>.OfInterpolated(
            Codecs.FLOAT,
            LerpFunction<float>.OfFloat(),
            FloatModifier.LIBRARY,
            LerpFunction<float>.OfDegrees(90.0F),
            toFloat: static value => value);

    public static readonly AttributeType<int> INTEGER =
        AttributeType<int>.OfInterpolated(
            Codecs.INT,
            LerpFunction<int>.OfInteger(),
            IntegerModifier.LIBRARY,
            toFloat: static value => value,
            toInt: static value => value);

    public static readonly AttributeType<MoonPhase> MOON_PHASE =
        AttributeType<MoonPhase>.OfNotInterpolated(MoonPhaseExtensions.CODEC);

    public static readonly AttributeType<Vector3> RGB_COLOR =
        AttributeType<Vector3>.OfInterpolated(
            ExtraCodecs.STRING_RGB_VEC3_COLOR,
            LerpFunction<Vector3>.OfColorVec3(),
            ColorModifier.RGB_LIBRARY);

    public static readonly AttributeType<Vector4> ARGB_COLOR =
        AttributeType<Vector4>.OfInterpolated(
            ExtraCodecs.STRING_ARGB_VEC4_COLOR,
            LerpFunction<Vector4>.OfColorVec4(),
            ColorModifier.ARGB_LIBRARY);

    public static readonly AttributeType<IParticleOptions> PARTICLE =
        AttributeType<IParticleOptions>.OfNotInterpolated(ParticleTypes.CODEC);

    public static readonly AttributeType<IReadOnlyList<AmbientParticle>> AMBIENT_PARTICLES =
        AttributeType<IReadOnlyList<AmbientParticle>>.OfInterpolated(
            AmbientParticle.CODEC.ListOf(),
            LerpFunction<AmbientParticle>.OfListCrossFade(
                static (particle, alpha) =>
                    particle with { Probability = particle.Probability * alpha }),
            ListModifier<AmbientParticle>.CreateLibrary(AmbientParticle.CODEC));

    internal static void RegisterAll()
    {
        Register("boolean", BOOLEAN);
        Register("float", FLOAT);
        Register("angle_degrees", ANGLE_DEGREES);
        Register("rgb_color", RGB_COLOR);
        Register("argb_color", ARGB_COLOR);
        Register("integer", INTEGER);
        Register("moon_phase", MOON_PHASE);
        Register("particle", PARTICLE);
        Register("ambient_particles", AMBIENT_PARTICLES);
    }

    private static void Register(string path, IAttributeType type)
    {
        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("asteria", path);
        RegistryKey<IAttributeType> key =
            RegistryKey<IAttributeType>.Create(BuiltInRegistries.AttributeType.Key, location);
        BuiltInRegistries.AttributeType.Register(key, type, RegistrationInfo.BuiltIn);
    }
}
