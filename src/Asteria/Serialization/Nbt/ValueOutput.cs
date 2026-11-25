namespace Asteria.Serialization.Nbt;

/// <summary>Typed writer for named values in one compound payload.</summary>
public interface ValueOutput
{
    void Store<T>(string name, Codec<T> codec, T value);
    void StoreNullable<T>(string name, Codec<T> codec, T? value);
    void Store<T>(MapCodec<T> codec, T value);
    void Store(CompoundTag tag);

    void PutBoolean(string name, bool value);
    void PutByte(string name, sbyte value);
    void PutShort(string name, short value);
    void PutInt(string name, int value);
    void PutLong(string name, long value);
    void PutFloat(string name, float value);
    void PutDouble(string name, double value);
    void PutString(string name, string value);
    void PutIntArray(string name, int[] value);

    TagValueOutput Child(string name);
    ValueOutputList ChildrenList(string name);
    TypedOutputList<T> List<T>(string name, Codec<T> codec);

    void Discard(string name);
    bool IsEmpty();
    NbtProblemReporter ProblemReporter { get; }
}

/// <summary>Writer for a list of compound child outputs.</summary>
public interface ValueOutputList
{
    TagValueOutput AddChild();
    void DiscardLast();
    bool IsEmpty();
}

/// <summary>Writer for values encoded by one codec into a list tag.</summary>
public interface TypedOutputList<T>
{
    void Add(T value);
    bool IsEmpty();
}
