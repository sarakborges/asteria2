namespace Asteria.Content.Storage;

/// <summary>
/// Read-only surface of a paletted container. LevelChunkSection exposes biome storage through
/// this contract while retaining a mutable container internally for world generation.
/// </summary>
public interface IPalettedContainerReadOnly<T> where T : class
{
    int Size { get; }

    PalettedContainerPackedData<T> Pack(PalettedContainerStrategy<T> strategy);

    T Get(int x, int y, int z);

    PalettedContainer<T> Recreate();

    IPalettedContainerReadOnly<T> Copy();

    bool MaybeHas(Func<T, bool> predicate);

    void ForEachInPalette(Action<T> action);

    void GetAll(Action<T> action);

    void Count(Action<T, int> output);
}
