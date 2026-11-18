namespace Asteria.Content.Registries;

/// <summary>
/// Registry index metadata paired with the registry's stable Holder.Reference.
/// The holder, not this wrapper, is the identity-bearing content reference.
/// </summary>
public sealed class RegistryEntry<T> where T : class
{
    public Holder<T>.Reference Holder { get; }
    public RegistryKey<T> Key => Holder.Key;
    public int Id { get; }
    public T Value => Holder.Value;

    internal RegistryEntry(Holder<T>.Reference holder, int id)
    {
        Holder = holder ?? throw new ArgumentNullException(nameof(holder));
        Id = id;
    }

    public override string ToString() => $"{Key} (runtime id: {Id})";
}
