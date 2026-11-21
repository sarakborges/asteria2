using System.Collections.ObjectModel;
using System.Numerics;

namespace Asteria.World.Attributes.Modifiers;

/// <summary>Parameters for the color-to-grayscale modifier.</summary>
public readonly record struct BlendToGray(float Brightness, float Factor);

/// <summary>Color modifier operations ported from the 26.3 attribute modifier libraries.</summary>
public static class ColorModifier
{
    public static readonly AttributeModifier<Vector3, Vector4> ALPHA_BLEND_RGB =
        Create<Vector3, Vector4>(AttributeOperationId.ALPHA_BLEND, AlphaBlendRgb, LerpFunction<Vector4>.OfColorVec4());
    public static readonly AttributeModifier<Vector3, Vector3> ADD_RGB =
        Create<Vector3, Vector3>(AttributeOperationId.ADD, AddRgb, LerpFunction<Vector3>.OfColorVec3());
    public static readonly AttributeModifier<Vector3, Vector3> SUBTRACT_RGB =
        Create<Vector3, Vector3>(AttributeOperationId.SUBTRACT, SubtractRgb, LerpFunction<Vector3>.OfColorVec3());
    public static readonly AttributeModifier<Vector3, Vector3> MULTIPLY_RGB =
        Create<Vector3, Vector3>(AttributeOperationId.MULTIPLY, static (a, b) => a * b, LerpFunction<Vector3>.OfColorVec3());
    public static readonly AttributeModifier<Vector3, BlendToGray> BLEND_TO_GRAY_RGB =
        Create<Vector3, BlendToGray>(AttributeOperationId.BLEND_TO_GRAY, BlendToGrayRgb, BlendToGrayLerp());

    public static readonly AttributeModifier<Vector4, Vector4> ALPHA_BLEND_ARGB =
        Create<Vector4, Vector4>(AttributeOperationId.ALPHA_BLEND, AlphaBlendArgb, LerpFunction<Vector4>.OfColorVec4());
    public static readonly AttributeModifier<Vector4, Vector3> ADD_ARGB =
        Create<Vector4, Vector3>(AttributeOperationId.ADD, AddArgb, LerpFunction<Vector3>.OfColorVec3());
    public static readonly AttributeModifier<Vector4, Vector3> SUBTRACT_ARGB =
        Create<Vector4, Vector3>(AttributeOperationId.SUBTRACT, SubtractArgb, LerpFunction<Vector3>.OfColorVec3());
    public static readonly AttributeModifier<Vector4, Vector4> MULTIPLY_ARGB =
        Create<Vector4, Vector4>(AttributeOperationId.MULTIPLY, static (a, b) => a * b, LerpFunction<Vector4>.OfColorVec4());
    public static readonly AttributeModifier<Vector4, BlendToGray> BLEND_TO_GRAY_ARGB =
        Create<Vector4, BlendToGray>(AttributeOperationId.BLEND_TO_GRAY, BlendToGrayArgb, BlendToGrayLerp());

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<Vector3>> RGB_LIBRARY { get; } =
        Library<Vector3>(
            (AttributeOperationId.ALPHA_BLEND, ALPHA_BLEND_RGB),
            (AttributeOperationId.ADD, ADD_RGB),
            (AttributeOperationId.SUBTRACT, SUBTRACT_RGB),
            (AttributeOperationId.MULTIPLY, MULTIPLY_RGB),
            (AttributeOperationId.BLEND_TO_GRAY, BLEND_TO_GRAY_RGB));

    public static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<Vector4>> ARGB_LIBRARY { get; } =
        Library<Vector4>(
            (AttributeOperationId.ALPHA_BLEND, ALPHA_BLEND_ARGB),
            (AttributeOperationId.ADD, ADD_ARGB),
            (AttributeOperationId.SUBTRACT, SUBTRACT_ARGB),
            (AttributeOperationId.MULTIPLY, MULTIPLY_ARGB),
            (AttributeOperationId.BLEND_TO_GRAY, BLEND_TO_GRAY_ARGB));

    private static AttributeModifier<TSubject, TArgument> Create<TSubject, TArgument>(
        AttributeOperationId operation,
        Func<TSubject, TArgument, TSubject> apply,
        LerpFunction<TArgument> lerp) =>
        new DelegateColorModifier<TSubject, TArgument>(operation, apply, lerp);

    private static IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> Library<T>(
        params (AttributeOperationId Operation, IAttributeModifier<T> Modifier)[] entries) =>
        new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>(
            entries.ToDictionary(entry => entry.Operation, entry => entry.Modifier));

    private static LerpFunction<BlendToGray> BlendToGrayLerp() =>
        new(static (alpha, from, to) => new BlendToGray(
            from.Brightness + (to.Brightness - from.Brightness) * alpha,
            from.Factor + (to.Factor - from.Factor) * alpha));

    private static Vector3 AlphaBlendRgb(Vector3 subject, Vector4 argument)
    {
        if (argument.W == 0.0F) return subject;
        return Vector3.Lerp(subject, new Vector3(argument.X, argument.Y, argument.Z), argument.W);
    }

    private static Vector4 AlphaBlendArgb(Vector4 destination, Vector4 source)
    {
        if (source.W == 1.0F) return source;
        if (source.W == 0.0F) return destination;
        float alpha = source.W + destination.W * (1.0F - source.W);
        float Blend(float d, float s) => (s * source.W + d * (alpha - source.W)) / alpha;
        return new Vector4(
            Blend(destination.X, source.X),
            Blend(destination.Y, source.Y),
            Blend(destination.Z, source.Z),
            alpha);
    }

    private static Vector3 AddRgb(Vector3 a, Vector3 b) =>
        Vector3.Min(a + b, Vector3.One);

    private static Vector3 SubtractRgb(Vector3 a, Vector3 b) =>
        Vector3.Max(a - b, Vector3.Zero);

    private static Vector4 AddArgb(Vector4 a, Vector3 b) =>
        new(MathF.Min(a.X + b.X, 1.0F), MathF.Min(a.Y + b.Y, 1.0F),
            MathF.Min(a.Z + b.Z, 1.0F), a.W);

    private static Vector4 SubtractArgb(Vector4 a, Vector3 b) =>
        new(MathF.Max(a.X - b.X, 0.0F), MathF.Max(a.Y - b.Y, 0.0F),
            MathF.Max(a.Z - b.Z, 0.0F), a.W);

    private static float Gray(Vector3 color) =>
        color.X * 0.3F + color.Y * 0.59F + color.Z * 0.11F;

    private static Vector3 BlendToGrayRgb(Vector3 subject, BlendToGray argument)
    {
        float gray = Math.Clamp(Gray(subject) * argument.Brightness, 0.0F, 1.0F);
        return Vector3.Lerp(subject, new Vector3(gray), argument.Factor);
    }

    private static Vector4 BlendToGrayArgb(Vector4 subject, BlendToGray argument)
    {
        Vector3 rgb = BlendToGrayRgb(new Vector3(subject.X, subject.Y, subject.Z), argument);
        return new Vector4(rgb, subject.W);
    }

    private sealed class DelegateColorModifier<TSubject, TArgument>(
        AttributeOperationId operation,
        Func<TSubject, TArgument, TSubject> apply,
        LerpFunction<TArgument> lerp) : AttributeModifier<TSubject, TArgument>
    {
        public override AttributeOperationId OperationId { get; } = operation;
        public override string GetSerializedName() => OperationId.GetSerializedName();
        public override TSubject Apply(TSubject subject, TArgument argument) => apply(subject, argument);
        public override LerpFunction<TArgument> ArgumentKeyframeLerp(EnvironmentAttribute<TSubject> attribute) => lerp;
    }
}
