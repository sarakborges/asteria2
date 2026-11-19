namespace Asteria.Content.Registries;

/// <summary>
/// Read-only mapping between runtime numeric IDs and registered values.
/// Implementations must be immutable after they are supplied to a paletted container.
/// </summary>
public interface IIdMap<T> where T : class
{
    int Count { get; }
    bool IsFrozen { get; }
    int GetId(T? value);
    T GetById(int id);
}
