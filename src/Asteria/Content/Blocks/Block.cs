using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// A block type definition. Its state definition precomputes every valid immutable block state.
/// </summary>
public class Block
{
    /// <summary>Global block-state ID map, populated as each state is constructed.</summary>
    public static BlockStateIdMap BlockStateRegistry { get; } = new();

    /// <summary>Returns the runtime ID of a state, using air's ID for null or unregistered states.</summary>
    public static int GetId(BlockState? blockState)
    {
        if (blockState is null)
            return 0;

        int id = BlockStateRegistry.GetId(blockState);
        return id == -1 ? 0 : id;
    }

    /// <summary>Resolves a runtime state ID, falling back to the default air state when absent.</summary>
    public static BlockState StateById(int idWithData) =>
        BlockStateRegistry.ById(idWithData) ?? Blocks.AIR.DefaultState;

    public BlockProperties Properties { get; }
    public StateDefinition StateDefinition { get; }
    public float ExplosionResistance { get; }
    public float Friction { get; }
    public float SpeedFactor { get; }
    public float JumpFactor { get; }
    public float BounceRestitution { get; }
    public float FallDistanceReduction { get; }
    public bool HasCollision { get; }
    public bool IsRandomlyTicking { get; }
    public bool DynamicShape { get; }
    public BlockState DefaultState { get; private set; } = null!;
    public Holder<Block>.Reference? BuiltInRegistryHolder { get; }

    public Block(BlockProperties properties)
        : this(properties, createIntrusiveRegistryHolder: true, configureState: null)
    {
    }

    protected Block(
        BlockProperties properties,
        bool createIntrusiveRegistryHolder,
        Action<StateDefinitionBuilder>? configureState = null)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        ExplosionResistance = Properties.ExplosionResistanceValue;
        Friction = Properties.FrictionValue;
        SpeedFactor = Properties.SpeedFactorValue;
        JumpFactor = Properties.JumpFactorValue;
        BounceRestitution = Properties.BounceRestitutionValue;
        FallDistanceReduction = Properties.FallDistanceReductionValue;
        HasCollision = Properties.HasCollisionValue;
        IsRandomlyTicking = Properties.IsRandomlyTickingValue;
        DynamicShape = Properties.DynamicShapeValue;

        // The reference implementation creates the intrusive holder during Block initialization,
        // before the constructor materializes the state definition.
        if (createIntrusiveRegistryHolder)
        {
            BuiltInRegistryHolder = BuiltInRegistries.Block.CreateIntrusiveHolder(this);
        }

        var builder = new StateDefinitionBuilder(this);
        CreateBlockStateDefinition(builder);
        configureState?.Invoke(builder);
        StateDefinition = builder.Build(this);
        DefaultState = StateDefinition.Any();
    }

    /// <summary>
    /// Equivalent to Block.createBlockStateDefinition: subclasses declare their state properties here.
    /// </summary>
    protected virtual void CreateBlockStateDefinition(StateDefinitionBuilder builder)
    {
    }

    protected void RegisterDefaultState(BlockState state)
    {
        DefaultState = state;
    }

    public override string ToString() =>
        $"Block{{{BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ?? "[unregistered]"}}}";
}
