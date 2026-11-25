namespace Asteria.Serialization.Nbt;

/// <summary>
/// Compound-backed ValueOutput implementation. Codec encoding uses the supplied
/// DynamicOps instance, enabling registry-aware codecs when RegistryOps is provided.
/// </summary>
public sealed class TagValueOutput : ValueOutput
{
    private readonly CompoundTag _output;
    private readonly DynamicOps<Tag> _ops;

    public NbtProblemReporter ProblemReporter { get; }

    private TagValueOutput(
        CompoundTag output,
        DynamicOps<Tag> ops,
        NbtProblemReporter problemReporter)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        ProblemReporter = problemReporter ?? throw new ArgumentNullException(nameof(problemReporter));
    }

    public static TagValueOutput Create(
        DynamicOps<Tag>? ops = null,
        NbtProblemReporter? problemReporter = null) =>
        new(
            new CompoundTag(),
            ops ?? NbtOps.INSTANCE,
            problemReporter ?? new NbtProblemReporter());

    public void Store<T>(string name, Codec<T> codec, T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(codec);

        DataResult<Tag> result = codec.EncodeStart(_ops, value);
        if (result.TryGetValueOrPartial(out Tag encoded))
        {
            ReportResult("encode field '" + name + "'", result, ProblemReporter.ForChild(name));
            _output.Put(name, encoded);
        }
        else
        {
            ReportResult("encode field '" + name + "'", result, ProblemReporter.ForChild(name));
        }
    }

    public void StoreNullable<T>(string name, Codec<T> codec, T? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(codec);
        if (value is null)
        {
            Discard(name);
            return;
        }

        Store(name, codec, value);
    }

    public void Store<T>(MapCodec<T> codec, T value)
    {
        ArgumentNullException.ThrowIfNull(codec);
        DataResult<Tag> result = codec.EncodeStart(_ops, value);
        if (result.TryGetValueOrPartial(out Tag encoded))
        {
            ReportResult("encode compound", result, ProblemReporter);
            if (encoded is CompoundTag compound)
            {
                foreach (KeyValuePair<string, Tag> entry in compound.Values)
                    _output.Put(entry.Key, entry.Value);
            }
            else
            {
                ProblemReporter.Report(
                    "encode compound",
                    $"MapCodec returned {encoded.GetTagType().PrettyName}, expected TAG_Compound.");
            }
        }
        else
        {
            ReportResult("encode compound", result, ProblemReporter);
        }
    }

    public void Store(CompoundTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        foreach (KeyValuePair<string, Tag> entry in tag.Values)
            _output.Put(entry.Key, entry.Value);
    }

    public void PutBoolean(string name, bool value) =>
        _output.PutBoolean(ValidateName(name), value);

    public void PutByte(string name, sbyte value) =>
        _output.PutByte(ValidateName(name), value);

    public void PutShort(string name, short value) =>
        _output.PutShort(ValidateName(name), value);

    public void PutInt(string name, int value) =>
        _output.PutInt(ValidateName(name), value);

    public void PutLong(string name, long value) =>
        _output.PutLong(ValidateName(name), value);

    public void PutFloat(string name, float value) =>
        _output.PutFloat(ValidateName(name), value);

    public void PutDouble(string name, double value) =>
        _output.PutDouble(ValidateName(name), value);

    public void PutString(string name, string value) =>
        _output.PutString(ValidateName(name), value);

    public void PutIntArray(string name, int[] value) =>
        _output.PutIntArray(ValidateName(name), value);

    public TagValueOutput Child(string name)
    {
        ValidateName(name);
        var child = new CompoundTag();
        _output.PutCompound(name, child);
        return new TagValueOutput(
            child,
            _ops,
            ProblemReporter.ForChild(name));
    }

    public ValueOutputList ChildrenList(string name)
    {
        ValidateName(name);
        var list = new ListTag();
        _output.PutList(name, list);
        return new TagValueOutputList(
            list,
            _ops,
            ProblemReporter.ForChild(name));
    }

    public TypedOutputList<T> List<T>(string name, Codec<T> codec)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(codec);
        var list = new ListTag();
        _output.PutList(name, list);
        return new TagTypedOutputList<T>(
            list,
            codec,
            _ops,
            ProblemReporter.ForChild(name));
    }

    public void Discard(string name) =>
        _output.Remove(ValidateName(name));

    public bool IsEmpty() =>
        _output.IsEmpty();

    public CompoundTag BuildResult() =>
        _output;

    private static string ValidateName(string name) =>
        name ?? throw new ArgumentNullException(nameof(name));

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

    private sealed class TagValueOutputList : ValueOutputList
    {
        private readonly ListTag _list;
        private readonly DynamicOps<Tag> _ops;
        private readonly NbtProblemReporter _reporter;

        public TagValueOutputList(
            ListTag list,
            DynamicOps<Tag> ops,
            NbtProblemReporter reporter)
        {
            _list = list;
            _ops = ops;
            _reporter = reporter;
        }

        public TagValueOutput AddChild()
        {
            var child = new CompoundTag();
            _list.AddTag(_list.Size(), child);
            return new TagValueOutput(
                child,
                _ops,
                _reporter.ForIndex(_list.Size() - 1));
        }

        public void DiscardLast()
        {
            if (!_list.IsEmpty())
                _list.Remove(_list.Size() - 1);
        }

        public bool IsEmpty() =>
            _list.IsEmpty();
    }

    private sealed class TagTypedOutputList<T> : TypedOutputList<T>
    {
        private readonly ListTag _list;
        private readonly Codec<T> _codec;
        private readonly DynamicOps<Tag> _ops;
        private readonly NbtProblemReporter _reporter;

        public TagTypedOutputList(
            ListTag list,
            Codec<T> codec,
            DynamicOps<Tag> ops,
            NbtProblemReporter reporter)
        {
            _list = list;
            _codec = codec;
            _ops = ops;
            _reporter = reporter;
        }

        public void Add(T value)
        {
            int index = _list.Size();
            DataResult<Tag> result = _codec.EncodeStart(_ops, value);
            if (result.TryGetValueOrPartial(out Tag encoded))
            {
                ReportResult("encode list element", result, _reporter.ForIndex(index));
                _list.AddTag(_list.Size(), encoded);
            }
            else
            {
                ReportResult("encode list element", result, _reporter.ForIndex(index));
            }
        }

        public bool IsEmpty() =>
            _list.IsEmpty();
    }
}
