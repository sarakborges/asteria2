using Asteria.Serialization;

namespace Asteria.World.Attributes.Modifiers;

public static class ListModifier<T>
{
    public static readonly AttributeModifier<IReadOnlyList<T>, IReadOnlyList<T>> APPEND =
        new AppendOperation();

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<IReadOnlyList<T>>> LIBRARY { get; } =
        CreateLibrary(null);

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<IReadOnlyList<T>>> CreateLibrary(
        Codec<T>? elementCodec) =>
        new Dictionary<AttributeOperationId, IAttributeModifier<IReadOnlyList<T>>>
        {
            [AttributeOperationId.APPEND] = elementCodec is null
                ? APPEND
                : new AppendOperation(elementCodec.ListOf())
        };

    private sealed class AppendOperation(Codec<IReadOnlyList<T>>? argumentCodec = null) :
        AttributeModifier<IReadOnlyList<T>, IReadOnlyList<T>>
    {
        public override Codec<IReadOnlyList<T>>? ArgumentCodec { get; } = argumentCodec;
        public override AttributeOperationId OperationId => AttributeOperationId.APPEND;
        public override string GetSerializedName() => OperationId.GetSerializedName();

        public override IReadOnlyList<T> Apply(
            IReadOnlyList<T> subject,
            IReadOnlyList<T> argument)
        {
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(argument);
            if (argument.Count == 0) return subject;
            if (subject.Count == 0) return argument;

            var result = new List<T>(subject.Count + argument.Count);
            result.AddRange(subject);
            result.AddRange(argument);
            return result;
        }

        public override LerpFunction<IReadOnlyList<T>> ArgumentKeyframeLerp(
            EnvironmentAttribute<IReadOnlyList<T>> attribute) =>
            attribute.Type.KeyframeLerp;
    }
}
