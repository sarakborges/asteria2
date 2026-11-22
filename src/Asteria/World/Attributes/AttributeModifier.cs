using Asteria.Serialization;

namespace Asteria.World.Attributes;

public enum AttributeOperationId
{
    OVERRIDE,
    ALPHA_BLEND,
    ADD,
    SUBTRACT,
    MULTIPLY,
    BLEND_TO_GRAY,
    MINIMUM,
    MAXIMUM,
    AND,
    NAND,
    OR,
    NOR,
    XOR,
    XNOR,
    APPEND,
    OVERLAY
}

public static class AttributeOperationIdExtensions
{
    public static string GetSerializedName(this AttributeOperationId operation) => operation switch
    {
        AttributeOperationId.OVERRIDE => "override",
        AttributeOperationId.ALPHA_BLEND => "alpha_blend",
        AttributeOperationId.ADD => "add",
        AttributeOperationId.SUBTRACT => "subtract",
        AttributeOperationId.MULTIPLY => "multiply",
        AttributeOperationId.BLEND_TO_GRAY => "blend_to_gray",
        AttributeOperationId.MINIMUM => "minimum",
        AttributeOperationId.MAXIMUM => "maximum",
        AttributeOperationId.AND => "and",
        AttributeOperationId.NAND => "nand",
        AttributeOperationId.OR => "or",
        AttributeOperationId.NOR => "nor",
        AttributeOperationId.XOR => "xor",
        AttributeOperationId.XNOR => "xnor",
        AttributeOperationId.APPEND => "append",
        AttributeOperationId.OVERLAY => "overlay",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown attribute operation.")
    };
}

/// <summary>Non-generic bridge used to store typed modifier entries in one map.</summary>
public interface IAttributeModifier<TSubject>
{
    AttributeOperationId OperationId { get; }
    string GetSerializedName();
    DataResult<MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>>> CreateEntryCodec(
        EnvironmentAttribute<TSubject> attribute);
    TSubject ApplyObject(TSubject subject, object? argument);
}

/// <summary>A modifier whose subject and argument types are explicit.</summary>
public abstract class AttributeModifier<TSubject, TArgument> : IAttributeModifier<TSubject>
{
    public abstract AttributeOperationId OperationId { get; }
    public abstract string GetSerializedName();
    public virtual Codec<TArgument>? ArgumentCodec => null;

    public virtual Codec<TArgument>? GetArgumentCodec(EnvironmentAttribute<TSubject> attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return ArgumentCodec;
    }

    public DataResult<MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>>> CreateEntryCodec(
        EnvironmentAttribute<TSubject> attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        Codec<TArgument>? argumentCodec = GetArgumentCodec(attribute);
        if (argumentCodec is null)
        {
            return DataResult<MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>>>.Error(
                () => $"Modifier '{GetSerializedName()}' has no argument codec for attribute '{attribute}'.");
        }

        MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>> codec =
            argumentCodec.FieldOf("argument").XMap<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>>(
                argument => new EnvironmentAttributeMap.Entry<TSubject, TArgument>(argument, this),
                entry => entry is EnvironmentAttributeMap.Entry<TSubject, TArgument> typed &&
                         ReferenceEquals(typed.Modifier, this)
                    ? typed.Argument
                    : throw new InvalidOperationException(
                        $"Entry does not belong to modifier '{GetSerializedName()}'."));

        return DataResult<MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<TSubject>>>.Success(codec);
    }

    public abstract TSubject Apply(TSubject subject, TArgument argument);
    public abstract LerpFunction<TArgument> ArgumentKeyframeLerp(
        EnvironmentAttribute<TSubject> attribute);

    public TSubject ApplyObject(TSubject subject, object? argument) =>
        Apply(subject, CastArgument(argument));

    private static TArgument CastArgument(object? value)
    {
        if (value is TArgument argument)
            return argument;

        if (value is null && default(TArgument) is null)
            return default!;

        throw new ArgumentException(
            $"Modifier argument must be {typeof(TArgument).Name}; received {value?.GetType().Name ?? "null"}.",
            nameof(value));
    }
}

public static class AttributeModifiers
{
    public static AttributeModifier<T, T> Override<T>() =>
        OverrideModifier<T>.Instance;

    private sealed class OverrideModifier<T> : AttributeModifier<T, T>
    {
        public static readonly OverrideModifier<T> Instance = new();

        private OverrideModifier() { }

        public override AttributeOperationId OperationId => AttributeOperationId.OVERRIDE;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override T Apply(T subject, T argument) => argument;

        public override Codec<T>? GetArgumentCodec(EnvironmentAttribute<T> attribute) =>
            attribute.ValueCodec;

        public override LerpFunction<T> ArgumentKeyframeLerp(
            EnvironmentAttribute<T> attribute) =>
            attribute.Type.KeyframeLerp;
    }
}
