using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

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

    public virtual FluidState GetFluidState(BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return global::Asteria.Content.Fluids.Fluids.EMPTY.DefaultFluidState;
    }

    public virtual bool IsRandomlyTickingState(BlockState state) => IsRandomlyTicking;

    public virtual VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos position) =>
        Shapes.Block();

    public virtual VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos position) =>
        HasCollision ? state.GetShape(level, position) : Shapes.Empty();

    public virtual bool IsCollisionShapeFullBlock(BlockState state, BlockGetter level, BlockPos position) =>
        IsShapeFullBlock(state.GetCollisionShape(level, position));

    public virtual VoxelShape GetVisualShape(BlockState state, BlockGetter level, BlockPos position) =>
        GetCollisionShape(state, level, position);

    public virtual VoxelShape GetBlockSupportShape(BlockState state, BlockGetter level, BlockPos position) =>
        GetCollisionShape(state, level, position);

    public virtual VoxelShape GetInteractionShape(BlockState state, BlockGetter level, BlockPos position) =>
        Shapes.Empty();

    protected virtual bool HasAnalogOutputSignal(BlockState state) => false;

    protected virtual bool IsSignalSource(BlockState state) => false;

    protected virtual bool ShouldRedstoneWireConnectTo(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction? direction) =>
        state.IsSignalSource() && direction is not null;

    protected virtual int GetSignal(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction direction) =>
        OwnSignal(state, level, position);

    protected virtual int OwnSignal(BlockState state, BlockGetter level, BlockPos position) => 0;

    protected virtual int GetDirectSignal(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction direction) => 0;

    internal bool ShouldRedstoneWireConnectToForState(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction? direction) =>
        ShouldRedstoneWireConnectTo(state, level, position, direction);

    internal int GetSignalForState(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction direction) =>
        GetSignal(state, level, position, direction);

    internal int OwnSignalForState(
        BlockState state,
        BlockGetter level,
        BlockPos position) =>
        OwnSignal(state, level, position);

    internal int GetDirectSignalForState(
        BlockState state,
        BlockGetter level,
        BlockPos position,
        Direction direction) =>
        GetDirectSignal(state, level, position, direction);

    internal bool HasAnalogOutputSignalForState(BlockState state) =>
        HasAnalogOutputSignal(state);

    internal bool IsSignalSourceForState(BlockState state) =>
        IsSignalSource(state);

    protected virtual BlockState Rotate(BlockState state, Rotation rotation) => state;

    protected virtual BlockState Mirror(BlockState state, Mirror mirror) => state;

    internal BlockState RotateState(BlockState state, Rotation rotation) =>
        Rotate(state, rotation);

    internal BlockState MirrorState(BlockState state, Mirror mirror) =>
        Mirror(state, mirror);

    public virtual VoxelShape GetOcclusionShape(BlockState state) =>
        state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero);

    public virtual bool UseShapeForLightOcclusion(BlockState state) => false;

    public virtual bool PropagatesSkylightDown(BlockState state) =>
        !IsShapeFullBlock(state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero)) &&
        state.GetFluidState().IsEmpty();

    public virtual int GetLightDampening(BlockState state) =>
        state.IsSolidRender ? 15 : state.PropagatesSkylightDown ? 0 : 1;

    public static bool IsFaceFull(VoxelShape shape, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(direction);
        return IsShapeFullBlock(shape.GetFaceShape(direction));
    }

    public static bool IsShapeFullBlock(VoxelShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return Shapes.Equal(Shapes.Block(), shape);
    }

    internal void InitializeStateCaches()
    {
        foreach (BlockState state in StateDefinition.PossibleStates)
            state.InitCache();
    }

    /// <summary>Copies every property shared by the source block into this block's default state.</summary>
    public BlockState WithPropertiesOf(BlockState source)
    {
        ArgumentNullException.ThrowIfNull(source);

        BlockState result = DefaultState;
        foreach (StateProperty property in source.Block.StateDefinition.Properties)
            result = property.CopyValue(source, result);

        return result;
    }

    public override string ToString() =>
        $"Block{{{BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ?? "[unregistered]"}}}";
}
