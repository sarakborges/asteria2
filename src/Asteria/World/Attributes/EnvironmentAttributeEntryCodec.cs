using Asteria.Serialization;

namespace Asteria.World.Attributes;

/// <summary>
/// Implements the compact override and dispatched full-entry representations for one attribute.
/// </summary>
internal sealed class EnvironmentAttributeEntryCodec<T> :
    Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>>
{
    private readonly Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>> _fullCodec;
    private readonly Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>>? _compactCodec;
    private readonly Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>> _decodeCodec;
    private readonly AttributeModifier<T, T> _overrideModifier = AttributeModifiers.Override<T>();

    public EnvironmentAttributeEntryCodec(EnvironmentAttribute<T> attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>> fullMapCodec =
            MapCodec<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>>.Dispatch(
                attribute.Type.ModifierCodec.FieldOf("modifier"),
                entry => DataResult<IAttributeModifier<T>>.Success(entry.Modifier),
                modifier => modifier.CreateEntryCodec(attribute));

        _fullCodec = fullMapCodec.Codec();

        _compactCodec = attribute.ValueCodec?.FlatXMap(
            value => DataResult<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>>.Success(
                new EnvironmentAttributeMap.Entry<T, T>(value, _overrideModifier)),
            entry => entry is EnvironmentAttributeMap.Entry<T, T> typed &&
                     ReferenceEquals(typed.Modifier, _overrideModifier)
                ? DataResult<T>.Success(typed.Argument)
                : DataResult<T>.Error("A compact environment attribute value must use override."));

        _decodeCodec = _compactCodec is null
            ? _fullCodec
            : _compactCodec.WithAlternative(_fullCodec, static entry => entry);
    }

    public override DataResult<TData> Encode<TData>(
        EnvironmentAttributeMap.IEnvironmentAttributeEntry<T> input,
        DynamicOps<TData> ops,
        TData prefix)
    {
        ArgumentNullException.ThrowIfNull(input);

        return ReferenceEquals(input.Modifier, _overrideModifier) && _compactCodec is not null
            ? _compactCodec.Encode(input, ops, prefix)
            : _fullCodec.Encode(input, ops, prefix);
    }

    public override DataResult<Pair<EnvironmentAttributeMap.IEnvironmentAttributeEntry<T>, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input) =>
        _decodeCodec.Decode(ops, input);
}
