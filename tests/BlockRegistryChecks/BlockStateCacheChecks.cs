using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Shapes;
using Asteria.World;

internal static class BlockStateCacheChecks
{
    public static void Run(BlockState lightEmittingState)
    {
        ArgumentNullException.ThrowIfNull(lightEmittingState);
        CheckAirStateCache();
        CheckSolidStateCache();
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
