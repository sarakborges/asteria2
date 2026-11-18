namespace Asteria.Content.Registries;

/// <summary>
/// Ownership context used by reference holders to determine whether a value can be serialized by a registry.
/// </summary>
public interface HolderOwner<T> where T : class
{
    bool CanSerializeIn(HolderOwner<T> owner);
}
