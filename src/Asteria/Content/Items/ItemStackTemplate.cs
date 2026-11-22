using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.Content.Items;

/// <summary>
/// Immutable, non-empty item stack description used before a world-backed ItemStack exists.
/// </summary>
public sealed class ItemStackTemplate
{
    private static readonly Codec<int> CountCodec = Codecs.INT.Validate(static count =>
        count is >= 1 and <= 99
            ? DataResult<int>.Success(count)
            : DataResult<int>.Error($"Value must be within range [1, 99]: {count}"));

    public static MapCodec<ItemStackTemplate> MAP_CODEC { get; } =
        CreateMapCodec(Item.CODEC);

    public static Codec<ItemStackTemplate> CODEC { get; } =
        MAP_CODEC.Codec().WithAlternative(
            Item.CODEC,
            static item => new ItemStackTemplate(item));

    public Holder<Item> ItemHolder { get; }
    public int Count { get; }
    public DataComponentPatch Components { get; }

    public ItemStackTemplate(Item item)
        : this(
            item?.BuiltInRegistryHolder ??
                throw new ArgumentNullException(nameof(item)),
            1,
            DataComponentPatch.EMPTY)
    {
    }

    public ItemStackTemplate(Holder<Item> item)
        : this(item, 1, DataComponentPatch.EMPTY)
    {
    }

    public ItemStackTemplate(Item item, int count)
        : this(
            item?.BuiltInRegistryHolder ??
                throw new ArgumentNullException(nameof(item)),
            count,
            DataComponentPatch.EMPTY)
    {
    }

    public ItemStackTemplate(Item item, DataComponentPatch components)
        : this(
            item?.BuiltInRegistryHolder ??
                throw new ArgumentNullException(nameof(item)),
            1,
            components)
    {
    }

    public ItemStackTemplate(
        Holder<Item> item,
        int count,
        DataComponentPatch components)
    {
        ItemHolder = item ?? throw new ArgumentNullException(nameof(item));
        Components = components ?? throw new ArgumentNullException(nameof(components));

        if (count == 0 || ReferenceEquals(item.Value, Items.AIR))
            throw new InvalidOperationException("Item must be non-empty");

        Count = count;
    }

    public ItemStackTemplate WithCount(int count) =>
        Count == count ? this : new ItemStackTemplate(ItemHolder, count, Components);

    private static MapCodec<ItemStackTemplate> CreateMapCodec(
        Codec<Holder<Item>> itemCodec) =>
        RecordCodecBuilder.MapCodec<ItemStackTemplate>(instance =>
            instance.Group(
                    itemCodec.FieldOf("id").ForGetter<ItemStackTemplate>(static value => value.ItemHolder),
                    CountCodec.OptionalFieldOf("count", 1)
                        .ForGetter<ItemStackTemplate>(static value => value.Count),
                    DataComponentPatch.CODEC.OptionalFieldOf(
                            "components",
                            DataComponentPatch.EMPTY)
                        .ForGetter<ItemStackTemplate>(static value => value.Components))
                .Apply(
                    instance,
                    static (item, count, components) =>
                        new ItemStackTemplate(item, count, components)));
}
