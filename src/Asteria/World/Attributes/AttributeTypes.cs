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

    internal static void RegisterAll()
    {
        Register("boolean", BOOLEAN);
        Register("float", FLOAT);
        Register("angle_degrees", ANGLE_DEGREES);
    }

    private static void Register(string path, IAttributeType type)
    {
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(path);
        RegistryKey<IAttributeType> key =
            RegistryKey<IAttributeType>.Create(BuiltInRegistries.AttributeType.Key, location);
        BuiltInRegistries.AttributeType.Register(key, type, RegistrationInfo.BuiltIn);
    }
}
