using System.Numerics;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.Attributes.Modifiers;

namespace Asteria.World.Attributes;

/// <summary>Built-in interpolation and modifier policies for implemented environment value types.</summary>
public static class AttributeTypes
{
    public static readonly AttributeType<bool> BOOLEAN =
        AttributeType<bool>.OfNotInterpolated(BooleanModifier.LIBRARY);

    public static readonly AttributeType<float> FLOAT =
        AttributeType<float>.OfInterpolated(
            LerpFunction<float>.OfFloat(),
            FloatModifier.LIBRARY,
            toFloat: static value => value);

    public static readonly AttributeType<float> ANGLE_DEGREES =
        AttributeType<float>.OfInterpolated(
            LerpFunction<float>.OfFloat(),
            FloatModifier.LIBRARY,
            LerpFunction<float>.OfDegrees(90.0F),
            toFloat: static value => value);

    public static readonly AttributeType<int> INTEGER =
        AttributeType<int>.OfInterpolated(
            LerpFunction<int>.OfInteger(),
            IntegerModifier.LIBRARY,
            toFloat: static value => value,
            toInt: static value => value);

    public static readonly AttributeType<Vector3> RGB_COLOR =
        AttributeType<Vector3>.OfInterpolated(
            LerpFunction<Vector3>.OfColorVec3(),
            ColorModifier.RGB_LIBRARY);

    public static readonly AttributeType<Vector4> ARGB_COLOR =
        AttributeType<Vector4>.OfInterpolated(
            LerpFunction<Vector4>.OfColorVec4(),
            ColorModifier.ARGB_LIBRARY);

    internal static void RegisterAll()
    {
        Register("boolean", BOOLEAN);
        Register("float", FLOAT);
        Register("angle_degrees", ANGLE_DEGREES);
        Register("rgb_color", RGB_COLOR);
        Register("argb_color", ARGB_COLOR);\n        Register("integer", INTEGER);
    }

    private static void Register(string path, IAttributeType type)
    {
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(path);
        RegistryKey<IAttributeType> key =
            RegistryKey<IAttributeType>.Create(BuiltInRegistries.AttributeType.Key, location);
        BuiltInRegistries.AttributeType.Register(key, type, RegistrationInfo.BuiltIn);
    }
}
