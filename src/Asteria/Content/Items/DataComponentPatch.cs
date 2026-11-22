using Asteria.Serialization;

namespace Asteria.Content.Items;

/// <summary>
/// Immutable item component patch. The current item domain has no registered component
/// types yet, so its only valid value is the empty patch.
/// </summary>
public sealed class DataComponentPatch
{
    public static readonly DataComponentPatch EMPTY = new();

    public static Codec<DataComponentPatch> CODEC { get; } =
        new EmptyDataComponentPatchCodec();

    private DataComponentPatch()
    {
    }

    private sealed class EmptyDataComponentPatchCodec : Codec<DataComponentPatch>
    {
        public override DataResult<TData> Encode<TData>(
            DataComponentPatch input,
            DynamicOps<TData> ops,
            TData prefix)
        {
            if (!ReferenceEquals(input, EMPTY))
            {
                return DataResult<TData>.Error(
                    "Only the empty item component patch is currently supported.");
            }

            return DataResult<TData>.Success(ops.EmptyMap);
        }

        public override DataResult<Pair<DataComponentPatch, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input) =>
            ops.GetMapValues(input).FlatMap(entries =>
                entries.Count == 0
                    ? DataResult<Pair<DataComponentPatch, TData>>.Success(
                        new Pair<DataComponentPatch, TData>(EMPTY, input))
                    : DataResult<Pair<DataComponentPatch, TData>>.Error(
                        "Item component entries require registered data component types."));
    }
}
