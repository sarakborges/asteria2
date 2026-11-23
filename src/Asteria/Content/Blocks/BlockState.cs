using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Content.Materials;
using Asteria.Core.Shapes;
using Asteria.World;
using Asteria.World.Pathfinding;

namespace Asteria.Content.Blocks;

public sealed class BlockState : StateHolder<Block, BlockState>
{
    private static readonly VoxelShape[] EMPTY_OCCLUSION_SHAPES = CreateOcclusionShapes(Shapes.Empty());
    private static readonly VoxelShape[] FULL_BLOCK_OCCLUSION_SHAPES = CreateOcclusionShapes(Shapes.Block());
    private static readonly Direction[] DIRECTIONS = Direction.GetValues();
    private static readonly SupportType[] SUPPORT_TYPES = Enum.GetValues<SupportType>();

    private FluidState? _fluidState;
    private VoxelShape? _collisionShape;
    private VoxelShape _occlusionShape = Shapes.Empty();
    private VoxelShape[] _occlusionShapesByFace = EMPTY_OCCLUSION_SHAPES;
    private bool _hasLargeCollisionShape;
    private bool _isCollisionShapeFullBlock;
    private bool[]? _faceSturdy;
    private bool _cacheInitialized;
    private readonly int _lightEmission;
    private readonly MapColor _mapColor;
    private readonly Func<BlockState, bool> _emissiveRendering;
    private readonly Func<BlockState, BlockPos, Vec3>? _offsetFunction;

    private readonly Func<BlockState, BlockGetter, BlockPos, bool> _isSuffocating;
    private readonly Func<BlockState, BlockGetter, BlockPos, AABB, bool> _isViewBlocking;
    private readonly Func<BlockState, BlockGetter, BlockPos, BlockPos?> _postProcess;

    private static VoxelShape[] CreateOcclusionShapes(VoxelShape shape)
    {
        var result = new VoxelShape[6];
        Array.Fill(result, shape);
        return result;
    }

    private static class CodecHolder
    {
        private static readonly Codec<Block> OwnerCodec =
            BuiltInRegistries.Block.ByNameCodec();

        public static readonly Codec<BlockState> FULL_CODEC =
            new BlockStateCodec(OwnerCodec, allowCompactBlockId: false);

        public static readonly Codec<BlockState> CODEC =
            new BlockStateCodec(OwnerCodec, allowCompactBlockId: true);
    }

    public static Codec<BlockState> FULL_CODEC => CodecHolder.FULL_CODEC;

    public static Codec<BlockState> CODEC => CodecHolder.CODEC;

    public static Codec<BlockState> CreateCodec(Codec<Block> ownerCodec) =>
        new BlockStateCodec(ownerCodec, allowCompactBlockId: true);

    public static Codec<BlockState> CreateFullCodec(Codec<Block> ownerCodec) =>
        new BlockStateCodec(ownerCodec, allowCompactBlockId: false);

    public Block Block => Owner;
    public new StateDefinition Definition => (StateDefinition)base.Definition;

    public bool IsAir { get; }
    public float DestroySpeed { get; }
    public bool RequiresCorrectToolForDrops { get; }
    public bool CanOcclude { get; }
    public bool IsRandomlyTicking { get; private set; }
    public bool IgnitedByLava { get; }
    public bool IsLiquid { get; }
    public PushReaction PistonPushReaction { get; }
    public bool IsReplaceable { get; }
    public bool ShouldSpawnTerrainParticles { get; }
    public bool IsSolid { get; private set; }
    public bool IsSolidRender { get; private set; }
    public bool PropagatesSkylightDown { get; private set; }
    public int LightDampening { get; private set; }
    public bool UseShapeForLightOcclusion { get; }
    public VoxelShape OcclusionShape => _occlusionShape;
    public bool IsLightPermeable => !IsSolidRender || LightDampening == 0;

    public int GetLightEmission() => _lightEmission;

    public MapColor GetMapColor(BlockGetter level, BlockPos position) =>
        _mapColor;

    public VoxelShape GetFaceOcclusionShape(Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        return _occlusionShapesByFace[direction.Get3DDataValue()];
    }

    public VoxelShape GetShape(BlockGetter level, BlockPos position) =>
        Block.GetShape(this, level, position);

    public BlockState Rotate(Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);
        return Block.RotateState(this, rotation);
    }

    public BlockState Mirror(Mirror mirror)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        return Block.MirrorState(this, mirror);
    }

    public float GetDestroySpeed(BlockGetter level, BlockPos position) => DestroySpeed;

    public bool CanBeReplaced() => IsReplaceable;

    public bool CanBeReplaced(Fluid fluid) =>
        Block.CanBeReplaced(this, fluid);

    public bool IsPathfindable(PathComputationType type) =>
        Block.IsPathfindable(this, type);

    public bool SkipRendering(BlockState neighborState, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(neighborState);
        ArgumentNullException.ThrowIfNull(direction);
        return Block.SkipRendering(this, neighborState, direction);
    }

    public int GetLightDampening() => LightDampening;

    public RenderShape GetRenderShape() => Block.GetRenderShape(this);

    public float GetShadeBrightness(BlockGetter level, BlockPos position) =>
        Block.GetShadeBrightness(this, level, position);

    public bool IsEmissiveRendering => _emissiveRendering(this);

    public Vec3 GetOffset(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return _offsetFunction?.Invoke(this, position) ?? Vec3.ZERO;
    }

    public bool HasOffsetFunction => _offsetFunction is not null;

    public bool IsSuffocating(BlockGetter level, BlockPos position) =>
        _isSuffocating(this, level, position);

    public bool IsViewBlocking(BlockGetter level, BlockPos position, AABB nearPlaneBox) =>
        _isViewBlocking(this, level, position, nearPlaneBox);

    public BlockPos? GetPostProcessPos(BlockGetter level, BlockPos position) =>
        _postProcess(this, level, position);

    public PushReaction GetPistonPushReaction() => PistonPushReaction;

    public VoxelShape GetCollisionShape(BlockGetter level, BlockPos position) =>
        _collisionShape ?? Block.GetCollisionShape(this, level, position);

    public VoxelShape GetVisualShape(BlockGetter level, BlockPos position) =>
        Block.GetVisualShape(this, level, position);

    public VoxelShape GetBlockSupportShape(BlockGetter level, BlockPos position) =>
        Block.GetBlockSupportShape(this, level, position);

    public VoxelShape GetInteractionShape(BlockGetter level, BlockPos position) =>
        Block.GetInteractionShape(this, level, position);

    public bool IsCollisionShapeFullBlock(BlockGetter level, BlockPos position) =>
        _collisionShape is not null
            ? _isCollisionShapeFullBlock
            : Block.IsCollisionShapeFullBlock(this, level, position);

    public bool IsFaceSturdy(BlockGetter level, BlockPos position, Direction direction) =>
        IsFaceSturdy(level, position, direction, SupportType.Full);

    public bool IsFaceSturdy(
        BlockGetter level,
        BlockPos position,
        Direction direction,
        SupportType supportType) =>
        _faceSturdy is null
            ? supportType.IsSupporting(this, level, position, direction)
            : _faceSturdy[direction.Get3DDataValue() * SUPPORT_TYPES.Length + (int)supportType];

    public bool HasLargeCollisionShape => _collisionShape is null || _hasLargeCollisionShape;

    internal BlockState(StateDefinition definition, object[] values)
        : base(definition, values)
    {
        BlockProperties properties = Definition.Owner.Properties;
        IsAir = properties.IsAir;
        DestroySpeed = properties.DestroyTimeValue;
        RequiresCorrectToolForDrops = properties.RequiresCorrectToolForDropsValue;
        CanOcclude = properties.CanOccludeValue;
        IgnitedByLava = properties.IgnitedByLavaValue;
        IsLiquid = properties.IsLiquidValue;
        PistonPushReaction = properties.PushReactionValue;
        IsReplaceable = properties.ReplaceableValue;
        ShouldSpawnTerrainParticles = properties.SpawnTerrainParticlesValue;
        UseShapeForLightOcclusion = Block.UseShapeForLightOcclusion(this);
        _lightEmission = properties.LightEmissionValue(this);
        _mapColor = properties.MapColorValue(this) ??
            throw new InvalidOperationException("A block state's map color cannot be null.");
        _emissiveRendering = properties.EmissiveRenderingValue;
        _offsetFunction = properties.OffsetFunctionValue;
        _isSuffocating = properties.IsSuffocatingValue;
        _isViewBlocking = properties.IsViewBlockingValue;
        _postProcess = properties.PostProcessValue;
        global::Asteria.Content.Blocks.Block.BlockStateRegistry.Add(this);
    }

    internal void InitCache()
    {
        if (_cacheInitialized)
            return;

        _fluidState = Block.GetFluidState(this);
        IsRandomlyTicking = Block.IsRandomlyTickingState(this);

        if (!Block.DynamicShape)
        {
            _collisionShape = Block.GetCollisionShape(this, EmptyBlockGetter.Instance, BlockPos.Zero);
            _hasLargeCollisionShape = IsLargeCollisionShape(_collisionShape);
            _isCollisionShapeFullBlock = Block.IsShapeFullBlock(_collisionShape);
            _faceSturdy = new bool[DIRECTIONS.Length * SUPPORT_TYPES.Length];
            foreach (Direction direction in DIRECTIONS)
            {
                foreach (SupportType supportType in SUPPORT_TYPES)
                {
                    int index = direction.Get3DDataValue() * SUPPORT_TYPES.Length + (int)supportType;
                    _faceSturdy[index] = supportType.IsSupporting(
                        this,
                        EmptyBlockGetter.Instance,
                        BlockPos.Zero,
                        direction);
                }
            }
        }

        IsSolid = CalculateLegacySolid();
        _occlusionShape = CanOcclude ? Block.GetOcclusionShape(this) : Shapes.Empty();
        IsSolidRender = Block.IsShapeFullBlock(_occlusionShape);

        if (_occlusionShape.IsEmpty())
        {
            _occlusionShapesByFace = EMPTY_OCCLUSION_SHAPES;
        }
        else if (IsSolidRender)
        {
            _occlusionShapesByFace = FULL_BLOCK_OCCLUSION_SHAPES;
        }
        else
        {
            _occlusionShapesByFace = new VoxelShape[6];
            foreach (Direction direction in Direction.GetValues())
                _occlusionShapesByFace[direction.Get3DDataValue()] = _occlusionShape.GetFaceShape(direction);
        }

        PropagatesSkylightDown = Block.PropagatesSkylightDown(this);
        LightDampening = Block.GetLightDampening(this);
        _cacheInitialized = true;
    }

    private bool CalculateLegacySolid()
    {
        if (Block.Properties.ForceSolidOnValue)
            return SolidDebugger.LogAndGet(Block.SolidDebugHolder, SolidDebugger.Reason.FORCE_SOLID_ON);
        else if (Block.Properties.ForceSolidOffValue)
            return SolidDebugger.LogAndGet(Block.SolidDebugHolder, SolidDebugger.Reason.FORCE_SOLID_OFF);
        else if (_collisionShape is null)
            return SolidDebugger.LogAndGet(Block.SolidDebugHolder, SolidDebugger.Reason.NULL_CACHE);
        else
        {
            VoxelShape shape = _collisionShape;
            if (shape.IsEmpty())
                return SolidDebugger.LogAndGet(Block.SolidDebugHolder, SolidDebugger.Reason.EMPTY_COLLISION_SHAPE);
            else
            {
                AABB bounds = shape.Bounds();
                if (bounds.GetSize() >= 0.7291666666666666)
                    return SolidDebugger.LogAndGet(
                        Block.SolidDebugHolder,
                        SolidDebugger.Reason.LARGE_ENOUGH_COLLISION_SHAPE);
                else
                    return bounds.GetYSize() >= 1.0
                        ? SolidDebugger.LogAndGet(
                            Block.SolidDebugHolder,
                            SolidDebugger.Reason.HIGH_ENOUGH_COLLISION_SHAPE)
                        : SolidDebugger.LogAndGet(Block.SolidDebugHolder, SolidDebugger.Reason.FALLTHROUGH);
            }
        }
    }

    private static bool IsLargeCollisionShape(VoxelShape shape)
    {
        if (shape.IsEmpty())
            return false;

        AABB bounds = shape.Bounds();
        return bounds.MinX < 0.0 || bounds.MinY < 0.0 || bounds.MinZ < 0.0 ||
            bounds.MaxX > 1.0 || bounds.MaxY > 1.0 || bounds.MaxZ > 1.0;
    }

    public bool Is(Block block) => ReferenceEquals(Block, block);

    public bool Is(TagKey<Block> tag) =>
        Block.BuiltInRegistryHolder?.Is(tag) ?? false;

    public FluidState GetFluidState() =>
        _fluidState ?? throw new InvalidOperationException(
            "BlockState cache has not been initialized. Initialize its owning block after construction.");
}
