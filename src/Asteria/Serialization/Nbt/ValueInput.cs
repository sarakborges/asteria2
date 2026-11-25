namespace Asteria.Serialization.Nbt;

/// <summary>
/// Typed view over named NBT values. A null read/list/child means the named value
/// is absent or not of the requested shape; codec failures remain DataResult errors.
/// </summary>
public interface ValueInput
{
    DataResult<T>? Read<T>(string name, Codec<T> codec);
    DataResult<T>? Read<T>(MapCodec<T> codec);

    TagValueInput? Child(string name);
    TagValueInput ChildOrEmpty(string name);
    TagValueInput RawChildOrEmpty(string name);

    ValueInputList? ChildrenList(string name);
    ValueInputList ChildrenListOrEmpty(string name);

    TypedInputList<T>? List<T>(string name, Codec<T> codec);
    TypedInputList<T> ListOrEmpty<T>(string name, Codec<T> codec);

    bool GetBooleanOr(string name, bool defaultValue);
    sbyte GetByteOr(string name, sbyte defaultValue);
    int GetShortOr(string name, short defaultValue);
    int? GetInt(string name);
    int GetIntOr(string name, int defaultValue);
    long? GetLong(string name);
    long GetLongOr(string name, long defaultValue);
    float GetFloatOr(string name, float defaultValue);
    double GetDoubleOr(string name, double defaultValue);
    string? GetString(string name);
    string GetStringOr(string name, string defaultValue);
    int[]? GetIntArray(string name);

    IReadOnlySet<string> KeySet { get; }

    NbtProblemReporter ProblemReporter { get; }
}
