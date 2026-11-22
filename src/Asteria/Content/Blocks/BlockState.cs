using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Content.Fluids;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

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
    public bool IsReplaceable { get; }
    public bool ShouldSpawnTerrainParticles { get; }
    public bool IsSolidRender { get; private set; }
    public bool PropagatesSkylightDown { get; private set; }
    public int LightDampening { get; private set; }
    public bool UseShapeForLightOcclusion { get; }
    public VoxelShape OcclusionShape => _occlusionShape;
    public bool IsLightPermeable => !IsSolidRender || LightDampening == 0;

    public int GetLightEmission() => _lightEmission;

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

    public bool HasAnalogOutputSignal() =>
        Block.HasAnalogOutputSignalForState(this);

    public bool IsSignalSource() =>
        Block.IsSignalSourceForState(this);

    public int GetLightDampening() => LightDampening;

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
        IsReplaceable = properties.ReplaceableValue;
        ShouldSpawnTerrainParticles = properties.SpawnTerrainParticlesValue;
        UseShapeForLightOcclusion = Block.UseShapeForLightOcclusion(this);
        _lightEmission = properties.LightEmissionValue(this);
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
