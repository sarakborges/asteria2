using System.Collections.ObjectModel;

namespace Asteria.World.Attributes.Modifiers;

public static class BooleanModifier
{
    public static readonly AttributeModifier<bool, bool> AND =
        new BooleanOperation(AttributeOperationId.AND, static (subject, argument) => argument && subject);
    public static readonly AttributeModifier<bool, bool> NAND =
        new BooleanOperation(AttributeOperationId.NAND, static (subject, argument) => !argument || !subject);
    public static readonly AttributeModifier<bool, bool> OR =
        new BooleanOperation(AttributeOperationId.OR, static (subject, argument) => argument || subject);
    public static readonly AttributeModifier<bool, bool> NOR =
        new BooleanOperation(AttributeOperationId.NOR, static (subject, argument) => !argument && !subject);
    public static readonly AttributeModifier<bool, bool> XOR =
        new BooleanOperation(AttributeOperationId.XOR, static (subject, argument) => argument ^ subject);
    public static readonly AttributeModifier<bool, bool> XNOR =
        new BooleanOperation(AttributeOperationId.XNOR, static (subject, argument) => argument == subject);

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<bool>> LIBRARY { get; } =
        new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<bool>>(
            new Dictionary<AttributeOperationId, IAttributeModifier<bool>>
            {
                [AttributeOperationId.AND] = AND,
                [AttributeOperationId.NAND] = NAND,
                [AttributeOperationId.OR] = OR,
                [AttributeOperationId.NOR] = NOR,
                [AttributeOperationId.XOR] = XOR,
                [AttributeOperationId.XNOR] = XNOR
            });

    private sealed class BooleanOperation(
        AttributeOperationId operationId,
        Func<bool, bool, bool> apply) : AttributeModifier<bool, bool>
    {
        public override AttributeOperationId OperationId { get; } = operationId;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override Asteria.Serialization.Codec<bool> ArgumentCodec => Asteria.Serialization.Codecs.BOOL;
        public override bool Apply(bool subject, bool argument) => apply(subject, argument);
        public override LerpFunction<bool> ArgumentKeyframeLerp(EnvironmentAttribute<bool> attribute) =>
            LerpFunction<bool>.OfConstant();
    }
}
