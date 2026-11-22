using System.Collections.ObjectModel;
using Asteria.Serialization;

namespace Asteria.World.Attributes;

public interface IAttributeType
{
    Type ValueType { get; }
}

public sealed class AttributeType<T> : IAttributeType
{
    private readonly Func<T, float>? _toFloat;
    private readonly Func<T, int>? _toInt;

    public Type ValueType => typeof(T);
    public Codec<T>? ValueCodec { get; }
    public Codec<IAttributeModifier<T>> ModifierCodec { get; }
    public IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> ModifierLibrary { get; }
    public LerpFunction<T> KeyframeLerp { get; }
    public LerpFunction<T> StateChangeLerp { get; }
    public LerpFunction<T> SpatialLerp { get; }
    public LerpFunction<T> PartialTickLerp { get; }

    private AttributeType(
        Codec<T>? valueCodec,
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> modifierLibrary,
        LerpFunction<T> keyframeLerp,
        LerpFunction<T> stateChangeLerp,
        LerpFunction<T> spatialLerp,
        LerpFunction<T> partialTickLerp,
        Func<T, float>? toFloat,
        Func<T, int>? toInt)
    {
        ValueCodec = valueCodec;
        ArgumentNullException.ThrowIfNull(modifierLibrary);
        ModifierLibrary = new ReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>(
            new Dictionary<AttributeOperationId, IAttributeModifier<T>>(modifierLibrary));
        ModifierCodec = CreateModifierCodec(ModifierLibrary);
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
        OfInterpolated(null, lerp, modifierLibrary, partialTickLerp, toFloat, toInt);

    public static AttributeType<T> OfInterpolated(
        Codec<T>? valueCodec,
        LerpFunction<T> lerp,
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> modifierLibrary,
        LerpFunction<T>? partialTickLerp = null,
        Func<T, float>? toFloat = null,
        Func<T, int>? toInt = null) =>
        new(valueCodec, modifierLibrary, lerp, lerp, lerp, partialTickLerp ?? lerp, toFloat, toInt);

    public static AttributeType<T> OfNotInterpolated(
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>? modifierLibrary = null) =>
        OfNotInterpolated(null, modifierLibrary);

    public static AttributeType<T> OfNotInterpolated(
        Codec<T>? valueCodec,
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>>? modifierLibrary = null) =>
        new(
            valueCodec,
            modifierLibrary ?? new Dictionary<AttributeOperationId, IAttributeModifier<T>>(),
            LerpFunction<T>.OfStep(1.0F),
            LerpFunction<T>.OfStep(0.0F),
            LerpFunction<T>.OfStep(0.5F),
            LerpFunction<T>.OfStep(0.0F),
            null,
            null);

    private static Codec<IAttributeModifier<T>> CreateModifierCodec(
        IReadOnlyDictionary<AttributeOperationId, IAttributeModifier<T>> modifierLibrary)
    {
        var modifiers = new Dictionary<AttributeOperationId, IAttributeModifier<T>>
        {
            [AttributeOperationId.OVERRIDE] = AttributeModifiers.Override<T>()
        };

        foreach (KeyValuePair<AttributeOperationId, IAttributeModifier<T>> entry in modifierLibrary)
        {
            if (!modifiers.TryAdd(entry.Key, entry.Value))
                throw new ArgumentException(
                    $"Modifier operation {entry.Key.GetSerializedName()} is reserved.",
                    nameof(modifierLibrary));
        }

        return Codecs.STRING.FlatXMap(
            name =>
            {
                foreach (IAttributeModifier<T> modifier in modifiers.Values)
                {
                    if (modifier.GetSerializedName() == name)
                        return DataResult<IAttributeModifier<T>>.Success(modifier);
                }

                return DataResult<IAttributeModifier<T>>.Error(
                    $"Unknown modifier '{name}' for {typeof(T).Name}.");
            },
            modifier =>
            {
                foreach (KeyValuePair<AttributeOperationId, IAttributeModifier<T>> entry in modifiers)
                {
                    if (ReferenceEquals(entry.Value, modifier))
                        return DataResult<string>.Success(entry.Key.GetSerializedName());
                }

                return DataResult<string>.Error(
                    $"Unregistered modifier '{modifier.GetSerializedName()}' for {typeof(T).Name}.");
            });
    }

    public void CheckAllowedModifier(IAttributeModifier<T> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (ReferenceEquals(modifier, AttributeModifiers.Override<T>()))
            return;

        if (!ModifierLibrary.Values.Any(candidate => ReferenceEquals(candidate, modifier)))
            throw new ArgumentException(
                $"Modifier {modifier.GetSerializedName()} is not valid for {typeof(T).Name}.",
                nameof(modifier));
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
