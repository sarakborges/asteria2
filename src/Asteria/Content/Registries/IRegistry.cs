using Asteria.Content.Identifiers;

namespace Asteria.Content.Registries;

/// <summary>
/// Common non-generic surface for registries stored inside the root registry.
/// </summary>
public interface IRegistry
{
    ResourceLocation Location { get; }
    int Count { get; }
    bool IsFrozen { get; }
    IReadOnlyCollection<ResourceLocation> KeySet();

    void BindAllTagsToEmpty();
    void Freeze();
    void Validate();
}
