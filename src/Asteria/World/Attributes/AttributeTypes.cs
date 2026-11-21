using Asteria.World.Attributes.Modifiers;

namespace Asteria.World.Attributes;

/// <summary>Boolean and float built-in attribute type policies.</summary>
public static class AttributeTypes
{
    public static readonly AttributeType<bool> BOOLEAN =
        AttributeType<bool>.OfNotInterpolated(BooleanModifier.LIBRARY);

    public static readonly AttributeType<float> FLOAT =
        AttributeType<float>.OfInterpolated(
            LerpFunction<float>.OfFloat(),
            FloatModifier.LIBRARY,
            toFloat: static value => value);
}
