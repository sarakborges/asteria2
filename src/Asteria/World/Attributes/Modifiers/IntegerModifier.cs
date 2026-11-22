using System.Collections.ObjectModel;

namespace Asteria.World.Attributes.Modifiers;

/// <summary>Integer modifier operations from the 26.3 attribute modifier library.</summary>
public static class IntegerModifier
{
    public static readonly AttributeModifier<int, int> ADD =
        new SimpleOperation(AttributeOperationId.ADD, static (subject, argument) => unchecked(subject + argument));
    public static readonly AttributeModifier<int, int> SUBTRACT =
        new SimpleOperation(AttributeOperationId.SUBTRACT, static (subject, argument) => unchecked(subject - argument));
    public static readonly AttributeModifier<int, int> MULTIPLY =
        new SimpleOperation(AttributeOperationId.MULTIPLY, static (subject, argument) => unchecked(subject * argument));
    public static readonly AttributeModifier<int, int> MINIMUM =
        new SimpleOperation(AttributeOperationId.MINIMUM, Math.Min);
    public static readonly AttributeModifier<int, int> MAXIMUM =
        new SimpleOperation(AttributeOperationId.MAXIMUM, Math.Max);

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<int>> LIBRARY { get; } =
        new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<int>>(
            new Dictionary<AttributeOperationId, IAttributeModifier<int>>
            {
                [AttributeOperationId.ADD] = ADD,
                [AttributeOperationId.SUBTRACT] = SUBTRACT,
                [AttributeOperationId.MULTIPLY] = MULTIPLY,
                [AttributeOperationId.MINIMUM] = MINIMUM,
                [AttributeOperationId.MAXIMUM] = MAXIMUM
            });

    private sealed class SimpleOperation(
        AttributeOperationId operationId,
        Func<int, int, int> apply) : AttributeModifier<int, int>
    {
        public override AttributeOperationId OperationId { get; } = operationId;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override Asteria.Serialization.Codec<int> ArgumentCodec => Asteria.Serialization.Codecs.INT;
        public override int Apply(int subject, int argument) => apply(subject, argument);
        public override LerpFunction<int> ArgumentKeyframeLerp(EnvironmentAttribute<int> attribute) =>
            LerpFunction<int>.OfInteger();
    }
}
