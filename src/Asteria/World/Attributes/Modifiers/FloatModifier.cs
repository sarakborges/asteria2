using System.Collections.ObjectModel;
using Asteria.Serialization;

namespace Asteria.World.Attributes.Modifiers;

public readonly record struct FloatWithAlpha(float Value, float Alpha)
{
    private static readonly Codec<FloatWithAlpha> FULL_CODEC =
        RecordCodecBuilder.Create<FloatWithAlpha>(instance =>
            instance.Group(
                Codecs.FLOAT.FieldOf("value").ForGetter<FloatWithAlpha>(static value => value.Value),
                Codecs.FloatRange(0.0F, 1.0F)
                    .OptionalFieldOf("alpha", 1.0F)
                    .ForGetter<FloatWithAlpha>(static value => value.Alpha))
            .Apply(instance, static (value, alpha) => new FloatWithAlpha(value, alpha)));

    public static readonly Codec<FloatWithAlpha> CODEC = new FloatWithAlphaCodec();

    private sealed class FloatWithAlphaCodec : Codec<FloatWithAlpha>
    {
        public override DataResult<TData> Encode<TData>(
            FloatWithAlpha input,
            DynamicOps<TData> ops,
            TData prefix) =>
            input.Alpha == 1.0F
                ? Codecs.FLOAT.Encode(input.Value, ops, prefix)
                : FULL_CODEC.Encode(input, ops, prefix);

        public override DataResult<Pair<FloatWithAlpha, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<float, TData>> scalar = Codecs.FLOAT.Decode(ops, input);
            if (scalar.TryGetResult(out Pair<float, TData> value))
                return DataResult<Pair<FloatWithAlpha, TData>>.Success(
                    new Pair<FloatWithAlpha, TData>(new FloatWithAlpha(value.First), value.Second),
                    scalar.Lifecycle);

            return FULL_CODEC.Decode(ops, input).Map(pair =>
                new Pair<FloatWithAlpha, TData>(pair.First, pair.Second));
        }
    }
}

public static class FloatModifier
{
    public static readonly AttributeModifier<float, FloatWithAlpha> ALPHA_BLEND = new AlphaBlendOperation();
    public static readonly AttributeModifier<float, float> ADD =
        new SimpleOperation(AttributeOperationId.ADD, static (subject, argument) => subject + argument);
    public static readonly AttributeModifier<float, float> SUBTRACT =
        new SimpleOperation(AttributeOperationId.SUBTRACT, static (subject, argument) => subject - argument);
    public static readonly AttributeModifier<float, float> MULTIPLY =
        new SimpleOperation(AttributeOperationId.MULTIPLY, static (subject, argument) => subject * argument);
    public static readonly AttributeModifier<float, float> MINIMUM =
        new SimpleOperation(AttributeOperationId.MINIMUM, Math.Min);
    public static readonly AttributeModifier<float, float> MAXIMUM =
        new SimpleOperation(AttributeOperationId.MAXIMUM, Math.Max);

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<float>> LIBRARY { get; } =
        new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<float>>(
            new Dictionary<AttributeOperationId, IAttributeModifier<float>>
            {
                [AttributeOperationId.ALPHA_BLEND] = ALPHA_BLEND,
                [AttributeOperationId.ADD] = ADD,
                [AttributeOperationId.SUBTRACT] = SUBTRACT,
                [AttributeOperationId.MULTIPLY] = MULTIPLY,
                [AttributeOperationId.MINIMUM] = MINIMUM,
                [AttributeOperationId.MAXIMUM] = MAXIMUM
            });

    private sealed class AlphaBlendOperation : AttributeModifier<float, FloatWithAlpha>
    {
        public override AttributeOperationId OperationId => AttributeOperationId.ALPHA_BLEND;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override Asteria.Serialization.Codec<FloatWithAlpha> ArgumentCodec => FloatWithAlpha.CODEC;

        public override float Apply(float subject, FloatWithAlpha argument) =>
            subject + argument.Alpha * (argument.Value - subject);

        public override LerpFunction<FloatWithAlpha> ArgumentKeyframeLerp(
            EnvironmentAttribute<float> attribute) =>
            new(static (alpha, from, to) => new FloatWithAlpha(
                from.Value + alpha * (to.Value - from.Value),
                from.Alpha + alpha * (to.Alpha - from.Alpha)));
    }

    private sealed class SimpleOperation(
        AttributeOperationId operationId,
        Func<float, float, float> apply) : AttributeModifier<float, float>
    {
        public override AttributeOperationId OperationId { get; } = operationId;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override Asteria.Serialization.Codec<float> ArgumentCodec => Asteria.Serialization.Codecs.FLOAT;
        public override float Apply(float subject, float argument) => apply(subject, argument);
        public override LerpFunction<float> ArgumentKeyframeLerp(EnvironmentAttribute<float> attribute) =>
            LerpFunction<float>.OfFloat();
    }
}
