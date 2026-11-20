namespace Asteria.Content.Registries;

/// <summary>
/// Lookup of registry references and named holder sets. Registration lookups may create
/// stable placeholders, matching the reference lookup contract.
/// </summary>
public interface HolderGetter<T> : HolderOwner<T> where T : class
{
    Holder<T>.Reference? Get(RegistryKey<T> key);
    HolderSet<T>.Named? Get(TagKey<T> tag);

    Holder<T>.Reference GetOrThrow(RegistryKey<T> key) =>
        Get(key) ?? throw new InvalidOperationException($"Missing element {key}");

    HolderSet<T>.Named GetOrThrow(TagKey<T> tag) =>
        Get(tag) ?? throw new InvalidOperationException($"Missing tag {tag}");

    Holder<T>? GetRandomElementOf(TagKey<T> tag, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Get(tag)?.GetRandomElement(random);
    }
}
