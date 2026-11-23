using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

internal static class BlockStateCacheChecks
{
    private sealed class CollisionShapeBlock : Block
    {
        private readonly VoxelShape _shape;

        public CollisionShapeBlock(VoxelShape shape, bool dynamicShape)
            : base(CreateProperties(dynamicShape), createIntrusiveRegistryHolder: false)
        {
            _shape = shape ?? throw new ArgumentNullException(nameof(shape));
            InitializeStateCaches();
        }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos position) =>
            _shape;

        private static BlockProperties CreateProperties(bool dynamicShape)
        {
            BlockProperties properties = BlockProperties.Of();
            if (dynamicShape)
                properties.DynamicShape();
            return properties;
        }
    }

    public static void Run(BlockState lightEmittingState)
    {
        ArgumentNullException.ThrowIfNull(lightEmittingState);
        CheckAirStateCache();
        CheckSolidStateCache();
        CheckLegacySolid();
        CheckConfiguredLightEmission(lightEmittingState);
    }

    private static void CheckAirStateCache()
    {
        Block airBlock = Blocks.AIR;
        BlockState air = airBlock.DefaultState;

        Require(air.IsAir && air.IsReplaceable, "Air must retain its air and replaceable state flags.");
        Require(!airBlock.HasCollision && !air.CanOcclude, "Air must disable collision and block occlusion.");
        Require(air.OcclusionShape.IsEmpty(), "Air must cache an empty occlusion shape.");
        Require(!air.IsSolidRender, "Air must not be treated as a solid-rendering block.");
        Require(air.PropagatesSkylightDown, "Air must propagate skylight downward.");
        Require(air.LightDampening == 0 && air.IsLightPermeable, "Air must not attenuate light.");
        Require(air.GetLightEmission() == 0, "Air must not emit block light by default.");

        foreach (Direction direction in Direction.GetValues())
            Require(
                air.GetFaceOcclusionShape(direction).IsEmpty(),
                "Air must use empty face-occlusion shapes for all directions.");
    }

    private static void CheckSolidStateCache()
    {
        BlockState stone = Blocks.STONE.DefaultState;

        Require(stone.CanOcclude, "Stone must allow block occlusion.");
        Require(ReferenceEquals(stone.OcclusionShape, Shapes.Block()), "A default solid block must cache the full-block occlusion shape.");
        Require(stone.IsSolidRender, "A full-block occlusion shape must enable solid rendering.");
        Require(!stone.PropagatesSkylightDown, "A full block shape must stop skylight propagation.");
        Require(stone.LightDampening == 15 && !stone.IsLightPermeable, "A solid-rendering block must attenuate light by fifteen.");
        Require(
            ReferenceEquals(
                stone.GetCollisionShape(EmptyBlockGetter.Instance, BlockPos.Zero),
                Shapes.Block()),
            "A non-dynamic solid block must cache its collision shape.");

        foreach (Direction direction in Direction.GetValues())
            Require(
                ReferenceEquals(stone.GetFaceOcclusionShape(direction), Shapes.Block()),
                "A full-block occlusion shape must use the shared full-block shape on every face.");
    }

    private static void CheckLegacySolid()
    {
        BlockState sufficientlyLarge = new CollisionShapeBlock(
            Shapes.Box(0.0, 0.0, 0.0, 0.73, 0.73, 0.73),
            dynamicShape: false).DefaultState;
        Require(sufficientlyLarge.IsSolid && !sufficientlyLarge.IsSolidRender,
            "Legacy solidity must use the collision-bounds average-size threshold independently of full-block solid rendering.");

        BlockState tooSmall = new CollisionShapeBlock(
            Shapes.Box(0.0, 0.0, 0.0, 0.72, 0.72, 0.72),
            dynamicShape: false).DefaultState;
        Require(!tooSmall.IsSolid,
            "A collision shape below the legacy average-size threshold and shorter than one block must not be solid.");

        BlockState fullHeight = new CollisionShapeBlock(
            Shapes.Box(0.0, 0.0, 0.0, 0.1, 1.0, 0.1),
            dynamicShape: false).DefaultState;
        Require(fullHeight.IsSolid,
            "A collision shape reaching one block in Y must be legacy-solid even when its average bounds size is below the threshold.");

        BlockState empty = new CollisionShapeBlock(
            Shapes.Empty(),
            dynamicShape: false).DefaultState;
        Require(!empty.IsSolid,
            "An empty static collision shape must never be legacy-solid.");

        VoxelShape dynamicShape = Shapes.Box(0.0, 0.0, 0.0, 0.9, 0.9, 0.9);
        BlockState dynamic = new CollisionShapeBlock(dynamicShape, dynamicShape: true).DefaultState;
        Require(!dynamic.IsSolid &&
                ReferenceEquals(dynamic.GetCollisionShape(EmptyBlockGetter.Instance, BlockPos.Zero), dynamicShape),
            "Dynamic-shape states have no cached collision-shape record and therefore remain non-solid under the legacy calculation.");
    }

    private static void CheckConfiguredLightEmission(BlockState state)
    {
        Require(state.GetLightEmission() == 11,
            "Block-state light emission must be computed from the owning block properties.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
