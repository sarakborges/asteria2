namespace Asteria.World.Attributes.Modifiers;

public static class ListModifier<T>
{
    public static readonly AttributeModifier<IReadOnlyList<T>, IReadOnlyList<T>> APPEND =
        new AppendOperation();

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<IReadOnlyList<T>>> LIBRARY { get; } =
        new Dictionary<AttributeOperationId, IAttributeModifier<IReadOnlyList<T>>>
        {
            [AttributeOperationId.APPEND] = APPEND
        };

    private sealed class AppendOperation :
        AttributeModifier<IReadOnlyList<T>, IReadOnlyList<T>>
    {
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
