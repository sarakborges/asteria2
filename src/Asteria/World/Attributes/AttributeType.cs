using System.Collections.ObjectModel;

namespace Asteria.World.Attributes;

/// <summary>Type-erased registry identity for typed attribute policies.</summary>
public interface IAttributeType
{
    Type ValueType { get; }
}

/// <summary>
/// Interpolation and modifier policy for one attribute value type.
/// Serialization codecs are supplied later by the local data pipeline.
/// </summary>
public sealed class AttributeType<T> : IAttributeType
{
    private readonly Func<T, float>? _toFloat;
    private readonly Func<T, int>? _toInt;

    public Type ValueType => typeof(T);
    public IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> ModifierLibrary { get; }
    public LerpFunction<T> KeyframeLerp { get; }
    public LerpFunction<T> StateChangeLerp { get; }
    public LerpFunction<T> SpatialLerp { get; }
    public LerpFunction<T> PartialTickLerp { get; }

    private AttributeType(
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> modifierLibrary,
        LerpFunction<T> keyframeLerp,
        LerpFunction<T> stateChangeLerp,
        LerpFunction<T> spatialLerp,
        LerpFunction<T> partialTickLerp,
        Func<T, float>? toFloat,
        Func<T, int>? toInt)
    {
        ArgumentNullException.ThrowIfNull(modifierLibrary);
        ModifierLibrary = new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>(
            new Dictionary<AttributeOperationId, IAttributeModifier<T>>(modifierLibrary));
        KeyframeLerp = keyframeLerp ?? throw new ArgumentNullException(nameof(keyframeLerp));
        StateChangeLerp = stateChangeLerp ?? throw new ArgumentNullException(nameof(stateChangeLerp));
        SpatialLerp = spatialLerp ?? throw new ArgumentNullException(nameof(spatialLerp));
        PartialTickLerp = partialTickLerp ?? throw new ArgumentNullException(nameof(partialTickLerp));
        _toFloat = toFloat;
        _toInt = toInt;
    }

    public static AttributeType<T> OfInterpolated(
        LerpFunction<T> lerp,
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> modifierLibrary,
        LerpFunction<T>? partialTickLerp = null,
        Func<T, float>? toFloat = null,
        Func<T, int>? toInt = null) =>
        new(
            modifierLibrary,
            lerp,
            lerp,
            lerp,
            partialTickLerp ?? lerp,
            toFloat,
            toInt);

    public static AttributeType<T> OfNotInterpolated(
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>? modifierLibrary = null) =>
        new(
            modifierLibrary ?? new Dictionary<AttributeOperationId, IAttributeModifier<T>>(),
            LerpFunction<T>.OfStep(1.0F),
            LerpFunction<T>.OfStep(0.0F),
            LerpFunction<T>.OfStep(0.5F),
            LerpFunction<T>.OfStep(0.0F),
            null,
            null);

    public void CheckAllowedModifier(IAttributeModifier<T> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (ReferenceEquals(modifier, AttributeModifiers.Override<T>()))
            return;

        if (!ModifierLibrary.Values.Any(candidate => ReferenceEquals(candidate, modifier)))
        {
            throw new ArgumentException(
                $"Modifier {modifier.GetSerializedName()} is not valid for {typeof(T).Name}.",
                nameof(modifier));
        }
    }

    public float ToFloat(T value) =>
        _toFloat is null
            ? throw new InvalidOperationException(
                $"{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)} cannot be represented as a float")
            : _toFloat(value);

    public int ToInt(T value) =>
        _toInt is null
            ? throw new InvalidOperationException(
                $"{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)} cannot be represented as an integer")
            : _toInt(value);
}
