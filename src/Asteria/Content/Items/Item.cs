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
            CreateCodec(BuiltInRegistries.Item);
    }

    public Holder<Item>.Reference BuiltInRegistryHolder { get; }

    public static Codec<Holder<Item>> CODEC => CodecHolder.CODEC;

    public Item()
        : this(BuiltInRegistries.Item)
    {
    }

    internal Item(Registry<Item> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        BuiltInRegistryHolder = registry.CreateIntrusiveHolder(this);
    }

    internal static Codec<Holder<Item>> CreateCodec(Registry<Item> registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.ByNameCodec()
            .XMap<Holder<Item>>(
                static item => item.BuiltInRegistryHolder,
                static holder => holder.Value)
            .Validate(static holder =>
                ReferenceEquals(holder.Value, Items.AIR)
                    ? DataResult<Holder<Item>>.Error("Item must not be asteria:air")
                    : DataResult<Holder<Item>>.Success(holder));
    }

    public override string ToString() =>
        $"Item{{{BuiltInRegistryHolder.KeyOrNull?.Location.ToString() ?? "[unregistered]"}}}";
}
