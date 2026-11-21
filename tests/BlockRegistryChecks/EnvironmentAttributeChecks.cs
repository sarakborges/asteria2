using System.Numerics;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World.Attributes;
using Asteria.World.Attributes.Modifiers;

internal static class EnvironmentAttributeChecks
{
    public static void Run()
    {
        CheckBuiltInAttributeTypeRegistry();
        CheckBuiltInEnvironmentAttributeRegistry();
        CheckAngleInterpolation();
        CheckColorAttributeTypes();
        CheckAttributeDefinitionAndRange();
        CheckTypeInterpolationAndNumericProjection();
        CheckTypedModifierLibraries();
        CheckEnvironmentAttributeMap();
    }

    private static void CheckBuiltInAttributeTypeRegistry()
    {
        Require(BuiltInRegistries.AttributeType.Location == ResourceLocation.Parse("asteria:attribute_type"),
            "The built-in attribute type registry must use the asteria:attribute_type key.");
        Require(ReferenceEquals(
                    BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:boolean")),
                    AttributeTypes.BOOLEAN) &&
                ReferenceEquals(
                    BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:float")),
                    AttributeTypes.FLOAT) &&
                ReferenceEquals(BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:angle_degrees")), AttributeTypes.ANGLE_DEGREES) &&
                ReferenceEquals(BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:rgb_color")), AttributeTypes.RGB_COLOR) &&
                ReferenceEquals(BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:argb_color")), AttributeTypes.ARGB_COLOR) &&
                BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:tri_state")) is null,
            "Built-in attribute type keys must resolve to the original typed policies.");
        Require(AttributeTypes.BOOLEAN.ValueType == typeof(bool) &&
                AttributeTypes.FLOAT.ValueType == typeof(float) &&
                AttributeTypes.ANGLE_DEGREES.ValueType == typeof(float) &&
                AttributeTypes.RGB_COLOR.ValueType == typeof(Vector3) &&
                AttributeTypes.ARGB_COLOR.ValueType == typeof(Vector4),
            "The type-erased registry boundary must preserve the runtime value type.");
        Require(BuiltInRegistries.AttributeType.AsHolderIdMap().ById(0) is not null &&
                BuiltInRegistries.AttributeType.AsHolderIdMap().ById(1) is not null &&
                BuiltInRegistries.AttributeType.AsHolderIdMap().ById(2) is not null &&
                BuiltInRegistries.AttributeType.Count == 5,
            "Attribute type registry IDs must expose both registered values.");
    }

    private static void CheckAttributeDefinitionAndRange()
    {
        RequireThrows<InvalidOperationException>(
            () => EnvironmentAttribute<float>.CreateBuilder(AttributeTypes.FLOAT).Build(),
            "An environment attribute must reject a missing default value.");

        EnvironmentAttribute<float> attribute = EnvironmentAttribute<float>
            .CreateBuilder(AttributeTypes.FLOAT)
            .DefaultValue(0.8F)
            .ValueRange(AttributeRange<float>.UnitFloat)
            .Syncable()
            .SpatiallyInterpolated()
            .FullResolutionBiomes()
            .Build();

        Require(attribute.DefaultValue == 0.8F, "An environment attribute must retain its default value.");
        Require(attribute.IsSyncable && attribute.IsPositional &&
                attribute.IsSpatiallyInterpolated && attribute.IsFullResolutionBiomes,
            "Environment attribute flags must preserve their builder defaults and enabled states.");
        Require(attribute.ValueRange.IsValid(0.25F), "A value inside an attribute range must validate.");
        Require(!attribute.ValueRange.TryValidate(1.25F, out string? error) &&
                error == "1.25 is not in range [0; 1]",
            "Float range validation must report the reference interval and rejected value.");
        Require(attribute.SanitizeValue(1.25F) == 1.0F &&
                attribute.SanitizeValue(-0.25F) == 0.0F,
            "Float range sanitization must clamp values to the inclusive interval.");

        EnvironmentAttribute<bool> nonPositional = EnvironmentAttribute<bool>
            .CreateBuilder(AttributeTypes.BOOLEAN)
            .DefaultValue(false)
            .NotPositional()
            .Build();
        Require(!nonPositional.IsPositional && !nonPositional.IsSpatiallyInterpolated,
            "Non-positional attributes must retain false spatial interpolation by default.");
    }

    private static void CheckTypeInterpolationAndNumericProjection()
    {
        Require(AttributeTypes.FLOAT.ToFloat(0.75F) == 0.75F,
            "The float attribute type must expose its float projection.");
        RequireThrows<InvalidOperationException>(
            () => AttributeTypes.FLOAT.ToInt(0.75F),
            "A float attribute type must reject an unregistered integer projection.");

        Require(Math.Abs(LerpFunction<float>.OfFloat().Apply(0.25F, 0.8F, 1.0F) - 0.85F) < 0.000001F,
            "Float interpolation must preserve alpha-weighted linear interpolation.");
        Require(AttributeTypes.BOOLEAN.KeyframeLerp.Apply(0.999F, false, true) == false &&
                AttributeTypes.BOOLEAN.KeyframeLerp.Apply(1.0F, false, true),
            "Non-interpolated keyframe transitions must switch at alpha one.");
        Require(AttributeTypes.BOOLEAN.StateChangeLerp.Apply(0.0F, false, true) &&
                !AttributeTypes.BOOLEAN.SpatialLerp.Apply(0.499F, false, true) &&
                AttributeTypes.BOOLEAN.SpatialLerp.Apply(0.5F, false, true) &&
                AttributeTypes.BOOLEAN.PartialTickLerp.Apply(0.0F, false, true),
            "The four non-interpolated contexts must use their distinct reference step thresholds.");
    }

    private static void CheckTypedModifierLibraries()
    {
        Require(BooleanModifier.AND.Apply(true, false) == false &&
                BooleanModifier.NAND.Apply(true, false) == true &&
                BooleanModifier.OR.Apply(false, true) == true &&
                BooleanModifier.NOR.Apply(false, true) == false &&
                BooleanModifier.XOR.Apply(true, true) == false &&
                BooleanModifier.XNOR.Apply(true, true) == true,
            "Boolean modifier truth tables must match the six reference operations.");

        Require(Math.Abs(FloatModifier.ADD.Apply(0.4F, 0.3F) - 0.7F) < 0.000001F &&
                Math.Abs(FloatModifier.SUBTRACT.Apply(0.4F, 0.3F) - 0.1F) < 0.000001F &&
                Math.Abs(FloatModifier.MULTIPLY.Apply(0.4F, 0.5F) - 0.2F) < 0.000001F &&
                FloatModifier.MINIMUM.Apply(0.4F, 0.3F) == 0.3F &&
                FloatModifier.MAXIMUM.Apply(0.4F, 0.3F) == 0.4F,
            "Float modifiers must preserve addition, subtraction, multiplication, minimum and maximum.");
        Require(Math.Abs(
                FloatModifier.ALPHA_BLEND.Apply(0.8F, new FloatWithAlpha(1.0F, 0.25F)) - 0.85F) < 0.000001F,
            "Alpha blending must interpolate from the subject toward the argument value by its alpha.");
        Require(FloatModifier.ALPHA_BLEND
                .ArgumentKeyframeLerp(
                    EnvironmentAttribute<float>.CreateBuilder(AttributeTypes.FLOAT).DefaultValue(0.0F).Build())
                .Apply(0.5F, new FloatWithAlpha(0.0F, 0.0F), new FloatWithAlpha(1.0F, 1.0F))
                == new FloatWithAlpha(0.5F, 0.5F),
            "Alpha-blend modifier keyframes must interpolate both value and alpha.");
    }

    private static void CheckEnvironmentAttributeMap()
    {
        EnvironmentAttribute<float> attribute = EnvironmentAttribute<float>
            .CreateBuilder(AttributeTypes.FLOAT)
            .DefaultValue(0.8F)
            .Build();

        EnvironmentAttributeMap multiplyMap = EnvironmentAttributeMap.CreateBuilder()
            .Modify(attribute, FloatModifier.MULTIPLY, 0.5F)
            .Build();
        Require(Math.Abs(multiplyMap.ApplyModifier(attribute, 0.8F) - 0.4F) < 0.000001F,
            "An attribute map must apply the typed modifier to its incoming value.");
        Require(!multiplyMap.Contains(EnvironmentAttribute<bool>
                .CreateBuilder(AttributeTypes.BOOLEAN).DefaultValue(false).Build()),
            "An attribute map must use attribute-definition identity for key membership.");

        EnvironmentAttributeMap overrideMap = EnvironmentAttributeMap.CreateBuilder()
            .Set(attribute, 0.25F)
            .Build();
        Require(overrideMap.ApplyModifier(attribute, 0.8F) == 0.25F,
            "Set must be equivalent to the override modifier.");
        Require(EnvironmentAttributeMap.Empty.ApplyModifier(attribute, 0.8F) == 0.8F,
            "A missing attribute entry must leave the lower-layer value unchanged.");

        EnvironmentAttributeMap.Builder mutableBuilder = EnvironmentAttributeMap.CreateBuilder()
            .Set(attribute, 0.6F);
        EnvironmentAttributeMap snapshot = mutableBuilder.Build();
        mutableBuilder.Set(attribute, 0.2F);
        Require(snapshot.ApplyModifier(attribute, 0.8F) == 0.6F,
            "Building a map must copy the builder entries into an immutable snapshot.");

        EnvironmentAttributeMap merged = EnvironmentAttributeMap.CreateBuilder()
            .PutAll(overrideMap)
            .PutAll(multiplyMap)
            .Build();
        Require(Math.Abs(merged.ApplyModifier(attribute, 0.8F) - 0.4F) < 0.000001F,
            "PutAll must replace an existing attribute entry with the later map entry.");

        RequireThrows<ArgumentException>(
            () => EnvironmentAttributeMap.CreateBuilder().Modify(
                attribute,
                new UnregisteredFloatModifier(),
                0.1F),
            "An attribute type must reject a modifier that is not part of its operation library.");
    }

    private sealed class UnregisteredFloatModifier : AttributeModifier<float, float>
    {
        public override AttributeOperationId OperationId => AttributeOperationId.ADD;
        public override string GetSerializedName() => "add";
        public override float Apply(float subject, float argument) => subject + argument;
        public override LerpFunction<float> ArgumentKeyframeLerp(EnvironmentAttribute<float> attribute) =>
            LerpFunction<float>.OfFloat();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }

    private static void CheckBuiltInEnvironmentAttributeRegistry()
    {
        Require(BuiltInRegistries.EnvironmentAttribute.Location == ResourceLocation.Parse("asteria:environment_attribute"),
            "The environment-attribute registry must use the Asteria key.");
        Require(BuiltInRegistries.EnvironmentAttribute.Count == 21,
            "The registry must contain only generic environmental definitions within the product scope.");
        Require(ReferenceEquals(
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:visual/fog_end_distance")),
                EnvironmentAttributes.FOG_END_DISTANCE) &&
            ReferenceEquals(
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:gameplay/creature_world_gen_spawn_probability")),
                EnvironmentAttributes.CREATURE_WORLD_GEN_SPAWN_PROBABILITY),
            "Built-in attribute keys must resolve to their canonical typed definitions.");
        Require(BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:audio/music_volume")) is null &&
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:gameplay/eyeblossom_open")) is null &&
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:gameplay/respawn_anchor_works")) is null,
            "Audio, bed and object-specific definitions must not enter the generic environment registry.");
        Require(EnvironmentAttributes.FOG_END_DISTANCE.DefaultValue == 1024.0F &&
            EnvironmentAttributes.FOG_END_DISTANCE.IsSpatiallyInterpolated &&
            EnvironmentAttributes.FOG_END_DISTANCE.IsSyncable &&
            ReferenceEquals(EnvironmentAttributes.FOG_END_DISTANCE.ValueRange, AttributeRange<float>.NonNegativeFloat),
            "Fog end distance must preserve its default, range, interpolation and sync flags.");
        Require(EnvironmentAttributes.SKY_LIGHT_LEVEL.DefaultValue == 15.0F &&
            !EnvironmentAttributes.SKY_LIGHT_LEVEL.IsPositional &&
            EnvironmentAttributes.SKY_LIGHT_LEVEL.IsSyncable &&
            EnvironmentAttributes.SKY_LIGHT_LEVEL.ValueRange.IsValid(15.0F) &&
            !EnvironmentAttributes.SKY_LIGHT_LEVEL.ValueRange.IsValid(15.01F),
            "Sky light level must be non-positional, synchronized and bounded to [0, 15].");
        Require(EnvironmentAttributes.CAN_START_RAID.DefaultValue &&
            !EnvironmentAttributes.CAN_START_RAID.IsSyncable &&
            EnvironmentAttributes.CAN_START_RAID.IsPositional,
            "Can-start-raid must preserve its default-true positional unsynchronized definition.");
        Require(!EnvironmentAttributes.FAST_LAVA.IsPositional && EnvironmentAttributes.FAST_LAVA.IsSyncable,
            "Fast lava must be non-positional and syncable.");
    }


    private static void CheckColorAttributeTypes()
    {
        Vector3 rgbFrom = new(0.1F, 0.2F, 0.3F);
        Vector3 rgbTo = new(0.9F, 0.8F, 0.7F);
        Vector3 rgbMid = AttributeTypes.RGB_COLOR.KeyframeLerp.Apply(0.5F, rgbFrom, rgbTo);
        Require(Vector3.Distance(rgbMid, new Vector3(0.5F, 0.5F, 0.5F)) < 0.000001F,
            "RGB color keyframes must linearly interpolate all three channels.");

        Vector4 argbFrom = new(0.0F, 0.2F, 0.4F, 0.2F);
        Vector4 argbTo = new(1.0F, 0.8F, 0.6F, 1.0F);
        Vector4 argbMid = AttributeTypes.ARGB_COLOR.KeyframeLerp.Apply(0.5F, argbFrom, argbTo);
        Require(Vector4.Distance(argbMid, new Vector4(0.5F, 0.5F, 0.5F, 0.6F)) < 0.000001F,
            "ARGB color keyframes must interpolate RGB and alpha channels.");

        Require(Vector3.Distance(
                ColorModifier.ADD_RGB.Apply(new Vector3(0.8F, 0.2F, 0.4F), new Vector3(0.4F, 0.3F, 0.8F)),
                new Vector3(1.0F, 0.5F, 1.0F)) < 0.000001F,
            "RGB addition must clamp each channel to one.");
        Require(Vector3.Distance(
                ColorModifier.SUBTRACT_RGB.Apply(new Vector3(0.2F, 0.8F, 0.4F), new Vector3(0.4F, 0.3F, 0.8F)),
                new Vector3(0.0F, 0.5F, 0.0F)) < 0.000001F,
            "RGB subtraction must clamp each channel to zero.");
        Require(Vector3.Distance(
                ColorModifier.MULTIPLY_RGB.Apply(new Vector3(0.5F, 0.4F, 0.2F), new Vector3(0.4F, 0.5F, 0.5F)),
                new Vector3(0.2F, 0.2F, 0.1F)) < 0.000001F,
            "RGB multiplication must multiply corresponding channels.");
        Require(Vector3.Distance(
                ColorModifier.ALPHA_BLEND_RGB.Apply(new Vector3(0.0F, 0.0F, 0.0F), new Vector4(1.0F, 0.5F, 0.0F, 0.25F)),
                new Vector3(0.25F, 0.125F, 0.0F)) < 0.000001F,
            "RGB alpha blending must use the alpha channel of its ARGB argument.");
        Require(Vector4.Distance(
                ColorModifier.ADD_ARGB.Apply(new Vector4(0.8F, 0.2F, 0.4F, 0.35F), new Vector3(0.4F, 0.3F, 0.8F)),
                new Vector4(1.0F, 0.5F, 1.0F, 0.35F)) < 0.000001F,
            "ARGB RGB addition must preserve the subject alpha channel.");
        Require(Vector4.Distance(
                ColorModifier.ALPHA_BLEND_ARGB.Apply(new Vector4(0.0F, 0.0F, 0.0F, 0.5F), new Vector4(1.0F, 0.5F, 0.0F, 0.5F)),
                new Vector4(2.0F / 3.0F, 1.0F / 3.0F, 0.0F, 0.75F)) < 0.000001F,
            "ARGB alpha blending must composite source over destination and compute resulting alpha.");
        Require(Vector3.Distance(
                ColorModifier.BLEND_TO_GRAY_RGB.Apply(new Vector3(1.0F, 0.0F, 0.0F), new BlendToGray(1.0F, 1.0F)),
                new Vector3(0.3F)) < 0.000001F,
            "Blend-to-gray must use the reference luminance weights.");

        Require(AttributeTypes.RGB_COLOR.ModifierLibrary.Count == 5 &&
                AttributeTypes.ARGB_COLOR.ModifierLibrary.Count == 5,
            "RGB and ARGB types must expose the five reference color modifier operations.");
    }

    private static void CheckAngleInterpolation()
    {
        Require(Math.Abs(AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.5F, 170.0F, -170.0F) - 180.0F) < 0.000001F &&
            AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.25F, 0.0F, 90.0F) == 90.0F &&
            AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.25F, 0.0F, 270.0F) == 270.0F,
            "Angle interpolation must wrap the shortest delta and snap at the 90-degree threshold.");
    }
}
