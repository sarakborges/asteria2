namespace Asteria.Content.Registries;

/// <summary>Registration-time lookup that can create stable key and tag placeholders.</summary>
public interface HolderGetter<T> where T : class
{
    Holder<T>.Reference GetOrThrow(RegistryKey<T> key);
    HolderSet<T>.Named GetOrThrow(TagKey<T> tag);
}
