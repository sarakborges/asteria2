namespace Asteria.Content.Storage;

/// <summary>
/// Common BitStorage contract shared by packed and zero-bit palette storage.
/// </summary>
public interface IBitStorage
{
    int BitsPerEntry { get; }
    int Size { get; }
    int LongCount { get; }

    int Get(int index);
    int GetAndSet(int index, int value);
    void Set(int index, int value);
    void Fill(int value);
    void GetAll(Action<int> output);
    void Unpack(int[] output);
    IBitStorage Copy();
}
