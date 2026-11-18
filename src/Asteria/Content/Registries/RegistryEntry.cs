namespace Asteria.Content.Registries;

/// <summary>
/// A stable reference to a registered value.
/// Numeric IDs are runtime indices and must not be persisted as content identifiers.
/// </summary>
public sealed class RegistryEntry<T> where T : class
{
    public RegistryKey<T> Key { get; }
    public int Id { get; }
    public T Value { get; }

    internal RegistryEntry(RegistryKey<T> key, int id, T value)
    {
        Key = key;
        Id = id;
        Value = value;
    }

    public override string ToString() => $"{Key} (runtime id: {Id})";
}
