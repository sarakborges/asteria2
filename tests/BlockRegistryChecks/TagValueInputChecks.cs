using Asteria.Serialization;
using Asteria.Serialization.Nbt;

internal static class TagValueInputChecks
{
    public static void Run()
    {
        CheckPrimitiveReadsAndDefaults();
        CheckCodecReadsAndDiagnostics();
        CheckNestedChildrenAndEmptySemantics();
        CheckCompoundChildrenLists();
        CheckTypedLists();
        CheckRegistryOpsInjection();
    }

    private static void CheckPrimitiveReadsAndDefaults()
    {
        var tag = new CompoundTag();
        tag.PutByte("byte", unchecked((sbyte)0xFE));
        tag.PutShort("short", unchecked((short)0xFEDC));
        tag.PutInt("int", 123);
        tag.PutLong("long", -456L);
        tag.PutFloat("float", 1.5f);
        tag.PutDouble("double", -2.25);
        tag.PutDouble("fractionalPositive", 0.5);
        tag.PutFloat("fractionalNegative", -0.5f);
        tag.PutDouble("fractionalNonZero", 1.5);
        tag.PutBoolean("enabled", true);
        tag.PutString("name", "overworld");
        tag.PutIntArray("levels", new[] { 1, -2, 3 });

        TagValueInput input = TagValueInput.Create(tag);
        Require(
            input.GetByteOr("byte", 0) == unchecked((sbyte)0xFE) &&
            input.GetShortOr("short", 0) == unchecked((short)0xFEDC) &&
            input.GetInt("int") == 123 &&
            input.GetIntOr("int", -1) == 123 &&
            input.GetLong("long") == -456L &&
            input.GetLongOr("long", 0) == -456L &&
            input.GetFloatOr("float", 0) == 1.5f &&
            input.GetDoubleOr("double", 0) == -2.25 &&
            input.GetBooleanOr("enabled", false) &&
            !input.GetBooleanOr("fractionalPositive", true) &&
            !input.GetBooleanOr("fractionalNegative", true) &&
            input.GetBooleanOr("fractionalNonZero", false) &&
            input.GetString("name") == "overworld" &&
            input.GetStringOr("missing", "fallback") == "fallback" &&
            input.GetIntArray("levels")!.SequenceEqual(new[] { 1, -2, 3 }),
            "ValueInput primitive accessors must preserve the numeric tag's Java-style conversions and exact string/array types.");

        Require(
            input.GetInt("missing") is null &&
            input.GetLong("missing") is null &&
            input.GetByteOr("missing", 7) == 7 &&
            input.GetBooleanOr("missing", true) &&
            input.GetIntArray("missing") is null,
            "Missing primitive values must use optional/null and default-value semantics.");

        Require(
            input.KeySet.SetEquals(new[] { "byte", "short", "int", "long", "float", "double", "fractionalPositive", "fractionalNegative", "fractionalNonZero", "enabled", "name", "levels" }),
            "ValueInput must expose the exact top-level compound key set.");
    }

    private static void CheckCodecReadsAndDiagnostics()
    {
        var root = new CompoundTag();
        root.PutInt("count", 42);
        root.PutString("badCount", "not-a-number");
        root.PutString("label", "stone");
        var reporter = new NbtProblemReporter();
        TagValueInput input = TagValueInput.Create(root, problemReporter: reporter);

        DataResult<int>? decoded = input.Read("count", Codecs.INT);
        Require(
            decoded is not null &&
            decoded.TryGetResult(out int count) &&
            count == 42,
            "ValueInput.Read must decode named values through the configured DynamicOps and Codec.");

        DataResult<int>? failed = input.Read("badCount", Codecs.INT);
        Require(
            failed is not null &&
            failed.IsError &&
            reporter.Problems.Count == 1 &&
            reporter.Problems[0].Path == "badCount",
            "Codec failures must remain observable as DataResult errors and be captured with their field path.");

        Require(
            input.Read("missing", Codecs.INT) is null,
            "Reading an absent codec field must return null without inventing a decode error.");

        MapCodec<PrimitiveRecord> recordCodec = RecordCodecBuilder.MapCodec<PrimitiveRecord>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("count").ForGetter<PrimitiveRecord>(value => value.Count),
                    Codecs.STRING.FieldOf("label").ForGetter<PrimitiveRecord>(value => value.Label))
                .Apply(instance, static (number, name) => new PrimitiveRecord(number, name)));
        root.PutString("label", "stone");
        DataResult<PrimitiveRecord>? compoundRead = input.Read(recordCodec);
        Require(
            compoundRead is not null &&
            compoundRead.TryGetResult(out PrimitiveRecord record) &&
            record == new PrimitiveRecord(42, "stone"),
            "ValueInput.Read(MapCodec) must decode a compound through DynamicOps map semantics.");
    }

    private static void CheckNestedChildrenAndEmptySemantics()
    {
        var root = new CompoundTag();
        var child = new CompoundTag();
        child.PutInt("value", 7);
        root.PutCompound("child", child);
        root.PutCompound("empty", new CompoundTag());
        root.PutInt("wrongChild", 9);

        TagValueInput input = TagValueInput.Create(root);
        Require(
            input.Child("child")?.GetInt("value") == 7 &&
            input.Child("missing") is null &&
            input.Child("wrongChild") is null,
            "Child must expose only named compound values and reject other payload types.");
        Require(
            input.ChildOrEmpty("missing").Read("anything", Codecs.INT) is null &&
            input.ChildOrEmpty("empty").Read("anything", Codecs.INT) is null,
            "ChildOrEmpty must return an unreadable empty input for absent and present-but-empty children.");
        Require(
            input.RawChildOrEmpty("empty").Read(
                MapCodec<EmptyRecord>.Unit(new EmptyRecord())) is not null,
            "RawChildOrEmpty must preserve a present empty compound for map codecs.");
        Require(
            input.RawChildOrEmpty("missing").Read(Codecs.INT.FieldOf("value")) is null,
            "RawChildOrEmpty must remain empty when the field does not exist.");
        Require(
            input.ProblemReporter.Problems.Any(problem => problem.Path == "wrongChild"),
            "Invalid child shapes must be reported at their named path.");
    }

    private static void CheckCompoundChildrenLists()
    {
        var root = new CompoundTag();
        var first = new CompoundTag();
        first.PutInt("index", 1);
        var second = new CompoundTag();
        second.PutInt("index", 2);
        var list = new ListTag();
        list.AddTag(0, first);
        list.AddTag(1, IntTag.ValueOf(3));
        list.AddTag(2, second);
        root.PutList("children", list);

        TagValueInput input = TagValueInput.Create(root);
        ValueInputList children = input.ChildrenList("children")!;
        Require(
            children.Count == 2 &&
            children[0].GetInt("index") == 1 &&
            children[1].GetInt("index") == 2 &&
            !children.IsEmpty(),
            "ChildrenList must expose compound children in order and report/skip non-compound elements.");
        Require(
            input.ChildrenListOrEmpty("missing").IsEmpty() &&
            input.ChildrenList("not-a-list") is null,
            "ChildrenList must distinguish missing/invalid fields from the empty-list convenience.");
        Require(
            input.ProblemReporter.Problems.Any(problem =>
                problem.Path == "children[1]" && problem.Operation == "read children list"),
            "Malformed compound-list entries must retain their index in diagnostics.");
    }

    private static void CheckTypedLists()
    {
        var root = new CompoundTag();
        var list = new ListTag();
        list.AddTag(0, IntTag.ValueOf(1));
        list.AddTag(1, IntTag.ValueOf(2));
        list.AddTag(2, StringTag.ValueOf("bad"));
        list.AddTag(3, IntTag.ValueOf(4));
        root.PutList("numbers", list);

        TagValueInput input = TagValueInput.Create(root);
        TypedInputList<int> decoded = input.ListOrEmpty("numbers", Codecs.INT);
        Require(
            decoded.Count == 3 &&
            decoded.SequenceEqual(new[] { 1, 2, 4 }) &&
            !decoded.IsEmpty(),
            "TypedInputList must decode valid entries in order and skip invalid elements after reporting them.");
        Require(
            input.ListOrEmpty("missing", Codecs.INT).IsEmpty() &&
            input.List("numbers", Codecs.INT)!.Count == 3,
            "Typed list helpers must expose optional and empty variants.");
        Require(
            input.ProblemReporter.Problems.Any(problem => problem.Path == "numbers[2]"),
            "Typed list codec failures must identify the element index.");
    }

    private static void CheckRegistryOpsInjection()
    {
        DynamicOps<Tag> ops = NbtOps.INSTANCE;
        TagValueInput input = TagValueInput.Create(new CompoundTag(), ops);
        Require(
            ReferenceEquals(input.Ops, ops),
            "ValueInput must retain caller-supplied DynamicOps so registry-aware codecs can be used without a parallel serialization path.");
    }

    private sealed record PrimitiveRecord(int Count, string Label);
    private sealed record EmptyRecord();
    private sealed record DummyValue(int Value);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
