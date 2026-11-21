namespace Asteria.World.Attributes.Modifiers;

/// <summary>List-valued environment attribute modifiers.</summary>
public static class ListModifier<T>
{
    public static readonly AttributeModifier<List<T>, List<T>> APPEND = new AppendOperation();

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<List<T>>> LIBRARY { get; } =
        new Dictionary<AttributeOperationId, IAttributeModifier<List<T>>>
        {
            [AttributeOperationId.APPEND] = APPEND
        };

    private sealed class AppendOperation : AttributeModifier<List<T>, List<T>>
    {
        public override AttributeOperationId OperationId => AttributeOperationId.APPEND;

        public override string GetSerializedName() => OperationId.GetSerializedName();

        public override List<T> Apply(List<T> subject, List<T> argument)
        {
            ArgumentNullException.ThrowIfNull(subject);
            ArgumentNullException.ThrowIfNull(argument);

            if (argument.Count == 0)
                return subject;

            if (subject.Count == 0)
                return argument;

            var result = new List<T>(subject.Count + argument.Count);
            result.AddRange(subject);
            result.AddRange(argument);
            return result;
        }

        public override LerpFunction<List<T>> ArgumentKeyframeLerp(
            EnvironmentAttribute<List<T>> attribute) =>
            attribute.Type.KeyframeLerp;
    }
}
