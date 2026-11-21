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
        CheckTriStateAndAngleInterpolation();
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
                ReferenceEquals(BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:tri_state")), AttributeTypes.TRI_STATE) &&
                ReferenceEquals(BuiltInRegistries.AttributeType.GetValue(ResourceLocation.Parse("asteria:angle_degrees")), AttributeTypes.ANGLE_DEGREES),
            "Built-in attribute type keys must resolve to the original typed policies.");
        Require(AttributeTypes.BOOLEAN.ValueType == typeof(bool) &&
                AttributeTypes.FLOAT.ValueType == typeof(float) &&
                AttributeTypes.TRI_STATE.ValueType == typeof(Asteria.Core.TriState) &&
                AttributeTypes.ANGLE_DEGREES.ValueType == typeof(float),
            "The type-erased registry boundary must preserve the runtime value type.");
        Require(BuiltInRegistries.AttributeType.AsHolderIdMap().ById(0) is not null &&
                BuiltInRegistries.AttributeType.AsHolderIdMap().ById(1) is not null &&
                BuiltInRegistries.AttributeType.AsHolderIdMap().ById(2) is not null &&
                BuiltInRegistries.AttributeType.AsHolderIdMap().ById(3) is not null,
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
        Require(BuiltInRegistries.EnvironmentAttribute.Count == 32,
            "The registry must contain the 32 built-in definitions whose value types are implemented.");
        Require(ReferenceEquals(
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:visual/fog_end_distance")),
                EnvironmentAttributes.FOG_END_DISTANCE) &&
            ReferenceEquals(
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:gameplay/eyeblossom_open")),
                EnvironmentAttributes.EYEBLOSSOM_OPEN) &&
            ReferenceEquals(
                BuiltInRegistries.EnvironmentAttribute.GetValue(ResourceLocation.Parse("asteria:gameplay/creature_world_gen_spawn_probability")),
                EnvironmentAttributes.CREATURE_WORLD_GEN_SPAWN_PROBABILITY),
            "Built-in attribute keys must resolve to their canonical typed definitions.");
        Require(EnvironmentAttributes.FOG_END_DISTANCE.DefaultValue == 1024.0F &&
            EnvironmentAttributes.FOG_END_DISTANCE.IsSpatiallyInterpolated &&
            EnvironmentAttributes.FOG_END_DISTANCE.IsSyncable &&
            ReferenceEquals(EnvironmentAttributes.FOG_END_DISTANCE.ValueRange, AttributeRange<float>.NonNegativeFloat),
            "Fog end distance must preserve its default, range, interpolation and sync flags.");
        Require(EnvironmentAttributes.MUSIC_VOLUME.DefaultValue == 1.0F &&
            !EnvironmentAttributes.MUSIC_VOLUME.IsSpatiallyInterpolated &&
            EnvironmentAttributes.MUSIC_VOLUME.IsSyncable,
            "Music volume must be syncable but not spatially interpolated.");
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

    private static void CheckTriStateAndAngleInterpolation()
    {
        Require(Asteria.Core.TriStateExtensions.From(true) == Asteria.Core.TriState.TRUE &&
            Asteria.Core.TriStateExtensions.From(false) == Asteria.Core.TriState.FALSE,
            "Tri-state conversion must map booleans to TRUE and FALSE.");
        Require(Asteria.Core.TriStateExtensions.ToBoolean(Asteria.Core.TriState.DEFAULT, true) &&
            !Asteria.Core.TriStateExtensions.ToBoolean(Asteria.Core.TriState.DEFAULT, false) &&
            Asteria.Core.TriStateExtensions.GetSerializedName(Asteria.Core.TriState.DEFAULT) == "default",
            "Tri-state DEFAULT must use the supplied fallback and serialize as default.");
        Require(EnvironmentAttributes.EYEBLOSSOM_OPEN.DefaultValue == Asteria.Core.TriState.DEFAULT,
            "Eyeblossom-open must default to tri-state DEFAULT.");
        Require(Math.Abs(AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.5F, 170.0F, -170.0F) - 180.0F) < 0.000001F &&
            AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.25F, 0.0F, 90.0F) == 90.0F &&
            AttributeTypes.ANGLE_DEGREES.PartialTickLerp.Apply(0.25F, 0.0F, 270.0F) == 270.0F,
            "Angle interpolation must wrap the shortest delta and snap at the 90-degree threshold.");
    }
}
