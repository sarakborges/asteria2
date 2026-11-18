using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

internal static class Program
{
    private enum TestAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    private static int Main()
    {
        try
        {
            RunChecks();
            Console.WriteLine("Block registry and block-state checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RunChecks()
    {
        Require(!Blocks.AIR.DefaultState.HasGlobalId, "State IDs must be assigned during bootstrap.");

        RegistryBootstrap.Initialize();

        Require(RegistryBootstrap.IsInitialized, "Bootstrap must complete.");
        Require(BuiltInRegistries.AreFrozen, "Built-in registries must be frozen.");
        Require(BuiltInRegistries.Block.Count == 2, "Two Asteria blocks should be registered.");
        Require(BuiltInRegistries.BlockStates.Count == 7, "Expected one air state and six test states.");

        CheckDefaultedBlockRegistry();
        CheckBlockStateDefinition();
        CheckBlockStateTransitions();
        CheckGlobalStateIds();
        CheckPropertyImplementations();
        CheckFrozenRegistry();

        var noPropertiesBlock = new Block(BlockProperties.Default);
        Require(
            noPropertiesBlock.StateDefinition.PossibleStates.Count == 1,
            "A block without properties must have exactly one state.");
    }

    private static void CheckDefaultedBlockRegistry()
    {
        ResourceLocation airId = ResourceLocation.Parse("asteria:air");
        ResourceLocation missingId = ResourceLocation.Parse("asteria:not_registered");

        Require(
            ReferenceEquals(BuiltInRegistries.Block.Get(airId), Blocks.AIR),
            "The air block must be registered.");
        Require(
            ReferenceEquals(BuiltInRegistries.Block.Get(missingId), Blocks.AIR),
            "Unknown block keys must resolve to the default air block.");
        Require(
            !BuiltInRegistries.Block.TryGetEntry(missingId, out _),
            "Exact lookups must not hide missing keys behind the default entry.");
        Require(
            BuiltInRegistries.Block.GetId(new Block(BlockProperties.Default)) == 0,
            "An unknown object must resolve to the default block ID.");
    }

    private static void CheckBlockStateDefinition()
    {
        StateDefinition definition = Blocks.TEST_BLOCK.StateDefinition;

        Require(definition.PossibleStates.Count == 6, "powered(2) x stage(3) must create six states.");
        Require(
            definition.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "powered", "stage" }),
            "State properties must be sorted by name.");
        Require(
            ReferenceEquals(definition.GetProperty("powered"), Blocks.TEST_POWERED),
            "Named property lookup must return the declared property instance.");

        BlockState defaultState = definition.DefaultState;
        Require(!defaultState.GetValue(Blocks.TEST_POWERED), "powered default must be false.");
        Require(defaultState.GetValue(Blocks.TEST_STAGE) == 1, "stage default must be one.");
        Require(
            ReferenceEquals(defaultState, Blocks.TEST_BLOCK.DefaultState),
            "The default state must be a stable precomputed instance.");
    }

    private static void CheckBlockStateTransitions()
    {
        BlockState defaultState = Blocks.TEST_BLOCK.DefaultState;

        BlockState changedState = defaultState
            .SetValue(Blocks.TEST_STAGE, 2)
            .SetValue(Blocks.TEST_POWERED, true);

        Require(changedState.GetValue(Blocks.TEST_POWERED), "powered must transition to true.");
        Require(changedState.GetValue(Blocks.TEST_STAGE) == 2, "stage must transition to two.");
        Require(
            ReferenceEquals(
                changedState,
                defaultState.SetValue(Blocks.TEST_POWERED, true).SetValue(Blocks.TEST_STAGE, 2)),
            "The same property combination must resolve to the same state object.");
        Require(
            ReferenceEquals(changedState, changedState.SetValue(Blocks.TEST_STAGE, 2)),
            "Setting a property to its existing value must preserve state identity.");
        Require(
            changedState.Cycle(Blocks.TEST_STAGE).GetValue(Blocks.TEST_STAGE) == 0,
            "Cycling the maximum integer value must wrap to the minimum.");
        Require(
            defaultState.Cycle(Blocks.TEST_POWERED).GetValue(Blocks.TEST_POWERED),
            "Cycling false must produce true.");

        Require(
            !changedState.TrySetValue(Blocks.TEST_STAGE, 9, out BlockState? invalidValueResult) &&
            invalidValueResult is null,
            "TrySetValue must reject values outside the property's allowed set.");

        BooleanProperty foreignPowered = BooleanProperty.Create("powered");
        Require(
            !changedState.TryGetValue(foreignPowered, out _),
            "A separate property instance must not be accepted even if its name matches.");
        RequireThrows<ArgumentException>(
            () => changedState.SetValue(foreignPowered, false),
            "SetValue must reject a property not owned by the block definition.");
    }

    private static void CheckGlobalStateIds()
    {
        BlockStateIdMap ids = BuiltInRegistries.BlockStates;

        for (int id = 0; id < ids.Count; id++)
        {
            BlockState state = ids.ById(id);
            Require(state.GlobalId == id, "State IDs must be contiguous and match insertion order.");
            Require(ReferenceEquals(ids.ById(state.GlobalId), state), "State ID reverse lookup must round-trip.");
            Require(ids.GetId(state) == id, "State-to-ID lookup must round-trip.");
        }

        Require(
            ids.GetId(Blocks.AIR.DefaultState) == 0,
            "The first registered block's singleton state must receive ID zero.");
        Require(
            !ids.TryGetById(-1, out _),
            "Invalid global state IDs must be reported as missing.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => ids.ById(-1),
            "Strict state ID lookup must reject invalid IDs.");
    }

    private static void CheckPropertyImplementations()
    {
        BooleanProperty lit = BooleanProperty.Create("lit");
        Require(lit.PossibleValues.SequenceEqual(new[] { false, true }), "Boolean values must be false then true.");
        Require(lit.TryParse("true", out bool parsedTrue) && parsedTrue, "Boolean parsing must accept true.");
        Require(!lit.TryParse("TRUE", out _), "Boolean parsing must be case-sensitive.");

        IntegerProperty age = IntegerProperty.Create("age", 2, 4);
        Require(age.PossibleValues.SequenceEqual(new[] { 2, 3, 4 }), "Integer values must be inclusive and ascending.");
        Require(age.TryParse("3", out int parsedAge) && parsedAge == 3, "Integer parsing must accept in-range values.");
        Require(!age.TryParse("5", out _), "Integer parsing must reject out-of-range values.");

        EnumProperty<TestAxis> axis = EnumProperty<TestAxis>.Create(
            "axis",
            new[] { TestAxis.Z, TestAxis.X },
            value => value.ToString().ToLowerInvariant());

        Require(
            axis.PossibleValues.SequenceEqual(new[] { TestAxis.X, TestAxis.Z }),
            "Enum values must be ordered by their underlying value.");
        Require(
            axis.TryParse("z", out TestAxis parsedAxis) && parsedAxis == TestAxis.Z,
            "Enum parsing must use the configured serialized name.");
    }

    private static void CheckFrozenRegistry()
    {
        RequireThrows<InvalidOperationException>(
            () => BuiltInRegistries.Block.Register(
                ResourceLocation.Parse("asteria:late_block"),
                new Block(BlockProperties.Default)),
            "A frozen block registry must reject new entries.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
