namespace Asteria.Content.Registries;

/// <summary>
/// Runtime ID mapping equivalent to Minecraft IdMap. The nullable lookup and throwing
/// helpers intentionally remain separate, as they are in the reference API.
/// </summary>
public interface IIdMap<T> : IEnumerable<T> where T : class
{
    int Count { get; }
    int GetId(T? value);
    T? ById(int id);

    T ByIdOrThrow(int id) =>
        ById(id) ?? throw new ArgumentException($"No value with id {id}.");

    int GetIdOrThrow(T? value)
    {
        int id = GetId(value);
        if (id == -1)
        {
            throw new ArgumentException($"Can't find an ID for '{value}' in this map.");
        }

        return id;
    }
}
