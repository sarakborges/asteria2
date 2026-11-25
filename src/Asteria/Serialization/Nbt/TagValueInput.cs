namespace Asteria.Serialization.Nbt;

/// <summary>
/// Compound-backed ValueInput implementation. Codec parsing uses the supplied
/// DynamicOps instance (NbtOps by default), so registry-aware callers can inject
/// RegistryOps without changing this storage adapter.
/// </summary>
public sealed class TagValueInput : ValueInput
{
    private static readonly CompoundTag EmptyTag = new();
    private readonly CompoundTag _input;
    private readonly DynamicOps<Tag> _ops;
    private readonly bool _canRead;

    public NbtProblemReporter ProblemReporter { get; }

    internal DynamicOps<Tag> Ops => _ops;

    public IReadOnlySet<string> KeySet =>
        _canRead
            ? new HashSet<string>(_input.Keys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    private TagValueInput(
        CompoundTag input,
        DynamicOps<Tag> ops,
        NbtProblemReporter problemReporter,
        bool canRead)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        ProblemReporter = problemReporter ?? throw new ArgumentNullException(nameof(problemReporter));
        _canRead = canRead;
    }

    public static TagValueInput Create(
        CompoundTag input,
        DynamicOps<Tag>? ops = null,
        NbtProblemReporter? problemReporter = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new TagValueInput(
            input,
            ops ?? NbtOps.INSTANCE,
            problemReporter ?? new NbtProblemReporter(),
            canRead: true);
    }

    public static ValueInputList Create(
        IEnumerable<CompoundTag> compounds,
        DynamicOps<Tag>? ops = null,
        NbtProblemReporter? problemReporter = null)
    {
        ArgumentNullException.ThrowIfNull(compounds);
        DynamicOps<Tag> actualOps = ops ?? NbtOps.INSTANCE;
        NbtProblemReporter reporter = problemReporter ?? new NbtProblemReporter();
        return new ValueInputList(compounds.Select((compound, index) =>
        {
            ArgumentNullException.ThrowIfNull(compound);
            return new TagValueInput(
                compound,
                actualOps,
                reporter.ForIndex(index),
                canRead: true);
        }));
    }

    public DataResult<T>? Read<T>(string name, Codec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(codec);
        if (!_canRead)
            return null;

        Tag? tag = _input.Get(name);
        if (tag is null)
            return null;

        DataResult<T> result = codec.Parse(_ops, tag);
        ReportResult("decode field '" + name + "'", result);
        return result;
    }

    public DataResult<T>? Read<T>(MapCodec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (!_canRead)
            return null;

        DataResult<T> result = codec.Parse(_ops, _input);
        ReportResult("decode compound", result);
        return result;
    }

    public TagValueInput? Child(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return null;

        Tag? tag = _input.Get(name);
        if (tag is CompoundTag compound)
        {
            return new TagValueInput(
                compound,
                _ops,
                ProblemReporter.ForChild(name),
                canRead: true);
        }

        if (tag is not null)
            ProblemReporter.ForChild(name).Report(
                "read child",
                $"Expected TAG_Compound, got {tag.GetTagType().PrettyName}.");

        return null;
    }

    public TagValueInput ChildOrEmpty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return CreateEmpty(name, canRead: false);

        Tag? tag = _input.Get(name);
        if (tag is not CompoundTag compound)
        {
            if (tag is not null)
            {
                ProblemReporter.ForChild(name).Report(
                    "read child",
                    $"Expected TAG_Compound, got {tag.GetTagType().PrettyName}.");
            }

            return CreateEmpty(name, canRead: false);
        }

        // A present-but-empty child is represented by the same unreadable input
        // used for a missing field. RawChildOrEmpty is the explicit codec path.
        return CreateEmpty(name, canRead: compound.Size() > 0, compound);
    }

    public TagValueInput RawChildOrEmpty(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return CreateEmpty(name, canRead: false);

        Tag? tag = _input.Get(name);
        if (tag is CompoundTag compound)
            return CreateEmpty(name, canRead: true, compound);

        if (tag is not null)
        {
            ProblemReporter.ForChild(name).Report(
                "read raw child",
                $"Expected TAG_Compound, got {tag.GetTagType().PrettyName}.");
        }

        return CreateEmpty(name, canRead: false);
    }

    public ValueInputList? ChildrenList(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return null;

        if (_input.Get(name) is not ListTag list)
        {
            if (_input.Get(name) is Tag wrongType)
            {
                ProblemReporter.ForChild(name).Report(
                    "read children list",
                    $"Expected TAG_List, got {wrongType.GetTagType().PrettyName}.");
            }

            return null;
        }

        var children = new List<TagValueInput>(list.Size());
        for (int index = 0; index < list.Size(); index++)
        {
            Tag element = list.Get(index);
            if (element is not CompoundTag compound)
            {
                ProblemReporter.ForChild(name).ForIndex(index).Report(
                    "read children list",
                    $"Expected TAG_Compound, got {element.GetTagType().PrettyName}.");
                continue;
            }

            children.Add(new TagValueInput(
                compound,
                _ops,
                ProblemReporter.ForChild(name).ForIndex(index),
                canRead: true));
        }

        return new ValueInputList(children);
    }

    public ValueInputList ChildrenListOrEmpty(string name) =>
        ChildrenList(name) ?? ValueInputList.Empty;

    public TypedInputList<T>? List<T>(string name, Codec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(codec);
        if (!_canRead)
            return null;

        if (_input.Get(name) is not ListTag list)
        {
            if (_input.Get(name) is Tag wrongType)
            {
                ProblemReporter.ForChild(name).Report(
                    "read typed list",
                    $"Expected TAG_List, got {wrongType.GetTagType().PrettyName}.");
            }

            return null;
        }

        var values = new List<T>(list.Size());
        for (int index = 0; index < list.Size(); index++)
        {
            DataResult<T> result = codec.Parse(_ops, list.Get(index));
            NbtProblemReporter reporter = ProblemReporter.ForChild(name).ForIndex(index);
            ReportResult("decode list element", result, reporter);
            if (result.TryGetValueOrPartial(out T value))
                values.Add(value);
        }

        return new TypedInputList<T>(values);
    }

    public TypedInputList<T> ListOrEmpty<T>(string name, Codec<T> codec) =>
        List(name, codec) ?? new TypedInputList<T>(Array.Empty<T>());

    public bool GetBooleanOr(string name, bool defaultValue) =>
        GetNumeric(name)?.DoubleValue() is double value
            ? value != 0.0
            : defaultValue;

    public sbyte GetByteOr(string name, sbyte defaultValue) =>
        GetNumeric(name)?.ByteValue() ?? defaultValue;

    public int GetShortOr(string name, short defaultValue) =>
        GetNumeric(name)?.ShortValue() ?? defaultValue;

    public int? GetInt(string name) =>
        GetNumeric(name)?.IntValue();

    public int GetIntOr(string name, int defaultValue) =>
        GetInt(name) ?? defaultValue;

    public long? GetLong(string name) =>
        GetNumeric(name)?.LongValue();

    public long GetLongOr(string name, long defaultValue) =>
        GetLong(name) ?? defaultValue;

    public float GetFloatOr(string name, float defaultValue) =>
        GetNumeric(name)?.FloatValue() ?? defaultValue;

    public double GetDoubleOr(string name, double defaultValue) =>
        GetNumeric(name)?.DoubleValue() ?? defaultValue;

    public string? GetString(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return null;

        if (_input.Get(name) is StringTag stringTag)
            return stringTag.Value;

        if (_input.Get(name) is Tag tag)
        {
            ProblemReporter.ForChild(name).Report(
                "read string",
                $"Expected TAG_String, got {tag.GetTagType().PrettyName}.");
        }

        return null;
    }

    public string GetStringOr(string name, string defaultValue) =>
        GetString(name) ?? defaultValue;

    public int[]? GetIntArray(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return null;

        if (_input.Get(name) is IntArrayTag array)
            return array.Data;

        if (_input.Get(name) is Tag tag)
        {
            ProblemReporter.ForChild(name).Report(
                "read int array",
                $"Expected TAG_Int_Array, got {tag.GetTagType().PrettyName}.");
        }

        return null;
    }

    private NumericTag? GetNumeric(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_canRead)
            return null;

        if (_input.Get(name) is NumericTag number)
            return number;

        if (_input.Get(name) is Tag tag)
        {
            ProblemReporter.ForChild(name).Report(
                "read number",
                $"Expected numeric tag, got {tag.GetTagType().PrettyName}.");
        }

        return null;
    }

    private TagValueInput CreateEmpty(
        string name,
        bool canRead,
        CompoundTag? compound = null) =>
        new(
            compound ?? EmptyTag,
            _ops,
            ProblemReporter.ForChild(name),
            canRead);

    private static void ReportResult<T>(
        string operation,
        DataResult<T> result,
        NbtProblemReporter reporter)
    {
        if (result.IsError)
        {
            reporter.Report(
                operation,
                result.ErrorMessage ?? "Codec operation failed.");
        }
    }

    private void ReportResult<T>(string operation, DataResult<T> result) =>
        ReportResult(operation, result, ProblemReporter);
}
