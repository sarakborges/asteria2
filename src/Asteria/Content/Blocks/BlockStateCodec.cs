using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.Content.Blocks;

/// <summary>
/// Codec for immutable block states. The compact form is the block identifier for its
/// default state; the full form is an object with id and non-default property values.
/// </summary>
internal sealed class BlockStateCodec : Codec<BlockState>
{
    private readonly Codec<Block> _ownerCodec;
    private readonly bool _allowCompactBlockId;

    public BlockStateCodec(Codec<Block> ownerCodec, bool allowCompactBlockId)
    {
        _ownerCodec = ownerCodec ?? throw new ArgumentNullException(nameof(ownerCodec));
        _allowCompactBlockId = allowCompactBlockId;
    }

    public override DataResult<TData> Encode<TData>(
        BlockState input,
        DynamicOps<TData> ops,
        TData prefix)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (_allowCompactBlockId && ReferenceEquals(input, input.Block.DefaultState))
            return _ownerCodec.Encode(input.Block, ops, prefix);

        return _ownerCodec.EncodeStart(ops, input.Block).FlatMap(encodedId =>
        {
            var entries = new List<KeyValuePair<TData, TData>>(2)
            {
                new(ops.CreateString("id"), encodedId)
            };

            BlockState defaultState = input.Block.DefaultState;
            var propertyEntries = new List<KeyValuePair<TData, TData>>();
            foreach (StateProperty property in input.Definition.Properties)
            {
                object value = input.GetValueObject(property);
                object defaultValue = defaultState.GetValueObject(property);
                if (Equals(value, defaultValue))
                    continue;

                propertyEntries.Add(new KeyValuePair<TData, TData>(
                    ops.CreateString(property.Name),
                    ops.CreateString(property.GetSerializedValue(value))));
            }

            if (propertyEntries.Count > 0)
            {
                entries.Add(new KeyValuePair<TData, TData>(
                    ops.CreateString("properties"),
                    ops.CreateMap(propertyEntries)));
            }

            return ops.MergeToMap(prefix, entries);
        }).SetLifecycle(Lifecycle.Stable());
    }

    public override DataResult<Pair<BlockState, TData>> Decode<TData>(
        DynamicOps<TData> ops,
        TData input)
    {
        if (_allowCompactBlockId)
        {
            DataResult<string> stringValue = ops.GetStringValue(input);
            if (stringValue.TryGetResult(out _))
            {
                return _ownerCodec.Parse(ops, input).Map(block =>
                    new Pair<BlockState, TData>(block.DefaultState, input));
            }
        }

        return ops.GetMap(input).FlatMap(map =>
            map.Get("id").FlatMap(encodedId => _ownerCodec.Parse(ops, encodedId))
                .FlatMap(block => DecodeProperties(ops, map, block))
                .Map(state => new Pair<BlockState, TData>(state, input)))
            .SetLifecycle(Lifecycle.Stable());
    }

    private static DataResult<BlockState> DecodeProperties<TData>(
        DynamicOps<TData> ops,
        MapLike<TData> input,
        Block block)
    {
        BlockState defaultState = block.DefaultState;
        if (!input.TryGet("properties", out TData encodedProperties))
            return DataResult<BlockState>.Success(defaultState);

        return ops.GetMap(encodedProperties).FlatMap(properties =>
        {
            DataResult<BlockState> decoded = DataResult<BlockState>.Success(defaultState);
            foreach (StateProperty property in defaultState.Definition.Properties)
            {
                if (!properties.TryGet(property.Name, out TData encodedValue))
                    continue;

                decoded = decoded.FlatMap(state =>
                    ops.GetStringValue(encodedValue).FlatMap(serializedValue =>
                    {
                        if (property.TrySetValue(state, serializedValue, out BlockState next))
                            return DataResult<BlockState>.Success(next);

                        return DataResult<BlockState>.Error(() =>
                            $"Invalid value '{serializedValue}' for property '{property.Name}' on block '{block}'");
                    }));
            }

            return decoded;
        });
    }
}
