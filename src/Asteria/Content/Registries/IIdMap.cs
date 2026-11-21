namespace Asteria.Content.Registries;

/// <summary>
/// Runtime ID mapping with distinct nullable lookup and throwing
/// helpers, following the reference API.
/// </summary>
public interface IIdMap<T> : IEnumerable<T> where T : class
{
    int Count { get; }
    int GetId(T? value);
    T? ById(int id);

    T ByIdOrThrow(int id) =>
        ById(id) ?? throw new ArgumentException($"No value with id {id}");

    int GetIdOrThrow(T? value)
    {
        int id = GetId(value);
        if (id == -1)
        {
            string valueDescription = value?.ToString() ?? "null";
            throw new ArgumentException($"Can't find id for '{valueDescription}' in map {this}");
        }

        return id;
    }
}
