using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.Content.Items;

/// <summary>
/// Registered item identity used by item-stack templates and item particles.
/// Gameplay behavior and item component prototypes are separate, later domains.
/// </summary>
public sealed class Item
{
    private static class CodecHolder
    {
        public static readonly Codec<Holder<Item>> CODEC =
            BuiltInRegistries.Item.ByNameCodec()
                .XMap<Holder<Item>>(
                    static item => item.BuiltInRegistryHolder,
                    static holder => holder.Value)
                .Validate(static holder =>
                    ReferenceEquals(holder.Value, Items.AIR)
                        ? DataResult<Holder<Item>>.Error("Item must not be asteria:air")
                        : DataResult<Holder<Item>>.Success(holder));
    }

    public Holder<Item>.Reference BuiltInRegistryHolder { get; }

    public static Codec<Holder<Item>> CODEC => CodecHolder.CODEC;

    public Item()
    {
        BuiltInRegistryHolder = BuiltInRegistries.Item.CreateIntrusiveHolder(this);
    }

    public override string ToString() =>
        $"Item{{{BuiltInRegistryHolder.KeyOrNull?.Location.ToString() ?? "[unregistered]"}}}";
}
