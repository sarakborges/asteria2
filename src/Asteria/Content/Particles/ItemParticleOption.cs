using Asteria.Content.Items;
using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>Item particle payload carrying the immutable item-stack template.</summary>
public sealed class ItemParticleOption : IParticleOptions
{
    public ParticleType<ItemParticleOption> Type { get; }
    public ItemStackTemplate ItemStack { get; }

    public ItemParticleOption(
        ParticleType<ItemParticleOption> type,
        Item item)
        : this(type, new ItemStackTemplate(item))
    {
    }

    public ItemParticleOption(
        ParticleType<ItemParticleOption> type,
        ItemStackTemplate itemStack)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        ItemStack = itemStack ?? throw new ArgumentNullException(nameof(itemStack));
    }

    public static MapCodec<ItemParticleOption> CreateCodec(
        ParticleType<ItemParticleOption> type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return ItemStackTemplate.CODEC.FieldOf("item").XMap(
            stack => new ItemParticleOption(type, stack),
            static options => options.ItemStack);
    }

    public ItemStackTemplate GetItem() => ItemStack;

    public ParticleType GetParticleType() => Type;
}
