using Asteria.Serialization;
using Asteria.Serialization.Nbt;

internal static class TagValueOutputChecks
{
    public static void Run()
    {
        CheckPrimitiveAndCodecWrites();
        CheckMapCodecAndCompoundStores();
        CheckNestedChildren();
        CheckChildrenListLifecycle();
        CheckTypedOutputLists();
        CheckDiscardAndNullableStores();
        CheckRoundTripThroughNbtIo();
    }

    private static void CheckPrimitiveAndCodecWrites()
    {
        var reporter = new NbtProblemReporter();
        TagValueOutput output = TagValueOutput.Create(problemReporter: reporter);
        output.PutBoolean("enabled", true);
        output.PutByte("byte", -3);
        output.PutShort("short", 12);
        output.PutInt("count", 34);
        output.PutLong("time", 56L);
        output.PutFloat("temperature", 1.5f);
        output.PutDouble("scale", 2.25);
        output.PutString("name", "stone");
        output.PutIntArray("levels", new[] { 1, 2, 3 });
        output.Store("codecValue", Codecs.INT, 99);

        CompoundTag result = output.BuildResult();
        Require(
            !output.IsEmpty() &&
            result.GetBooleanOr("enabled", false) &&
            result.GetByteOr("byte", 0) == -3 &&
            result.GetShortOr("short", 0) == 12 &&
            result.GetIntOr("count", 0) == 34 &&
            result.GetLongOr("time", 0) == 56L &&
            result.GetFloatOr("temperature", 0) == 1.5f &&
            result.GetDoubleOr("scale", 0) == 2.25 &&
            result.GetStringOr("name", "") == "stone" &&
            result.GetIntArray("levels")!.SequenceEqual(new[] { 1, 2, 3 }) &&
            result.GetIntOr("codecValue", 0) == 99 &&
            !reporter.HasProblems,
            "ValueOutput primitive and codec writers must create their exact NBT scalar/array values.");
    }

    private static void CheckMapCodecAndCompoundStores()
    {
        MapCodec<OutputRecord> codec = RecordCodecBuilder.MapCodec<OutputRecord>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("count").ForGetter<OutputRecord>(value => value.Count),
                    Codecs.STRING.FieldOf("label").ForGetter<OutputRecord>(value => value.Label))
                .Apply(instance, static (count, label) => new OutputRecord(count, label)));
        TagValueOutput output = TagValueOutput.Create();
        output.Store(codec, new OutputRecord(5, "five"));
        var stored = new CompoundTag();
        stored.PutBoolean("active", true);
        output.Store(stored);

        CompoundTag result = output.BuildResult();
        Require(
            result.GetIntOr("count", -1) == 5 &&
            result.GetStringOr("label", "") == "five" &&
            result.GetBooleanOr("active", false),
            "ValueOutput must merge MapCodec fields and stored compound entries into the current root.");
    }

    private static void CheckNestedChildren()
    {
        TagValueOutput output = TagValueOutput.Create();
        TagValueOutput child = output.Child("child");
        child.PutInt("value", 7);
        child.Child("nested").PutString("name", "inner");

        CompoundTag result = output.BuildResult();
        Require(
            result.GetCompoundOrEmpty("child").GetIntOr("value", -1) == 7 &&
            result.GetCompoundOrEmpty("child").GetCompoundOrEmpty("nested").GetStringOr("name", "") == "inner",
            "Child ValueOutput instances must write directly into their parent compound.");
    }

    private static void CheckChildrenListLifecycle()
    {
        TagValueOutput output = TagValueOutput.Create();
        ValueOutputList children = output.ChildrenList("children");
        TagValueOutput first = children.AddChild();
        first.PutInt("id", 1);
        TagValueOutput discarded = children.AddChild();
        discarded.PutInt("id", 2);
        TagValueOutput last = children.AddChild();
        last.PutInt("id", 3);
        children.DiscardLast();

        ListTag list = (ListTag)output.BuildResult().Get("children")!;
        Require(
            list.Size() == 2 &&
            ((CompoundTag)list.Get(0)).GetIntOr("id", -1) == 1 &&
            ((CompoundTag)list.Get(1)).GetIntOr("id", -1) == 2 &&
            !children.IsEmpty(),
            "ChildrenList must append compounds, support discardLast and preserve earlier entries.");
        var empty = (ValueOutputList)TagValueOutput.Create().ChildrenList("empty");
        Require(empty.IsEmpty(), "A new children-list writer must report empty.");
    }

    private static void CheckTypedOutputLists()
    {
        var reporter = new NbtProblemReporter();
        TagValueOutput output = TagValueOutput.Create(problemReporter: reporter);
        TypedOutputList<int> numbers = output.List("numbers", Codecs.INT);
        numbers.Add(1);
        numbers.Add(2);
        TypedOutputList<int> constrained = output.List(
            "constrained",
            Codecs.INT.Validate(value => value >= 0
                ? DataResult<int>.Success(value)
                : DataResult<int>.Error(() => "negative values are not allowed")));
        constrained.Add(3);
        constrained.Add(-4);

        CompoundTag result = output.BuildResult();
        Require(
            ((ListTag)result.Get("numbers")!).Get(0).AsInt() == 1 &&
            ((ListTag)result.Get("numbers")!).Get(1).AsInt() == 2 &&
            ((ListTag)result.Get("constrained")!).Size() == 1 &&
            ((ListTag)result.Get("constrained")!).Get(0).AsInt() == 3 &&
            reporter.Problems.Any(problem => problem.Path == "constrained[1]"),
            "Typed output lists must encode values through their codec and report/skip invalid elements.");
    }

    private static void CheckDiscardAndNullableStores()
    {
        TagValueOutput output = TagValueOutput.Create();
        output.PutInt("remove", 1);
        output.StoreNullable("nullable", Codecs.STRING, null);
        output.Discard("remove");
        Require(
            output.IsEmpty() &&
            output.BuildResult().Size() == 0,
            "Discard and null stores must leave no named field in the output compound.");
    }

    private static void CheckRoundTripThroughNbtIo()
    {
        TagValueOutput output = TagValueOutput.Create();
        output.PutString("id", "furnace");
        output.Child("data").PutInt("burnTime", 120);
        output.ChildrenList("items").AddChild().PutString("item", "iron_ingot");
        output.List("counters", Codecs.INT).Add(4);

        using var stream = new MemoryStream();
        NbtIo.WriteCompound(output.BuildResult(), stream, leaveOpen: true);
        stream.Position = 0;
        CompoundTag decoded = NbtIo.ReadCompound(stream, new NbtAccounter(4096), leaveOpen: true);
        Require(
            decoded.GetCompoundOrEmpty("data").GetIntOr("burnTime", -1) == 120 &&
            ((CompoundTag)((ListTag)decoded.Get("items")!).Get(0)).GetStringOr("item", "") == "iron_ingot" &&
            ((ListTag)decoded.Get("counters")!).Get(0).AsInt() == 4,
            "ValueOutput results must compose with the ordinary NBT persistence APIs.");
    }

    private sealed record OutputRecord(int Count, string Label);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
