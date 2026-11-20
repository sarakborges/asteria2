namespace Asteria.Content.Registries;

/// <summary>
/// Serialization context that decides whether it can serialize values belonging to another owner.
/// </summary>
public interface HolderOwner<T> where T : class
{
    bool CanSerializeIn(HolderOwner<T> owner);
}
