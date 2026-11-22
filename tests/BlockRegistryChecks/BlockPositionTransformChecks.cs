using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Core;
using Asteria.Core.Mathematics;
using Asteria.Serialization;
using Asteria.World;
using Asteria.World.LevelGen;

internal static class BlockPositionTransformChecks
{
    public static void Run()
    {
        CheckAxisCycles();
        CheckBlockPositionRotation();
        CheckRotationOperations();
        CheckRotationCodecsAndRandom();
        CheckMirrorOperations();
        CheckBlockStateTransforms();
    }

    private static void CheckAxisCycles()
    {
        AxisCycle[] cycles = Enum.GetValues<AxisCycle>();
        Require(cycles.Length == 3 &&
                cycles[0] == AxisCycle.None &&
                cycles[1] == AxisCycle.Forward &&
                cycles[2] == AxisCycle.Backward,
            "AxisCycle values must preserve NONE/FORWARD/BACKWARD source order.");

        Require(
            AxisCycle.None.Cycle(2, 3, 5, Direction.Axis.X) == 2 &&
            AxisCycle.Forward.Cycle(2, 3, 5, Direction.Axis.X) == 5 &&
            AxisCycle.Backward.Cycle(2, 3, 5, Direction.Axis.X) == 3,
            "AxisCycle integer coordinate selection must match each source permutation.");
        Require(
            AxisCycle.None.Cycle(2.5, 3.5, 5.5, Direction.Axis.Y) == 3.5 &&
            AxisCycle.Forward.Cycle(2.5, 3.5, 5.5, Direction.Axis.Y) == 2.5 &&
            AxisCycle.Backward.Cycle(2.5, 3.5, 5.5, Direction.Axis.Y) == 5.5,
            "AxisCycle double coordinate selection must match each source permutation.");

        Require(
            ReferenceEquals(AxisCycle.None.Cycle(Direction.Axis.Z), Direction.Axis.Z) &&
            ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.X), Direction.Axis.Y) &&
            ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.Y), Direction.Axis.Z) &&
            ReferenceEquals(AxisCycle.Forward.Cycle(Direction.Axis.Z), Direction.Axis.X) &&
            ReferenceEquals(AxisCycle.Backward.Cycle(Direction.Axis.X), Direction.Axis.Z) &&
            ReferenceEquals(AxisCycle.Backward.Cycle(Direction.Axis.Y), Direction.Axis.X) &&
            ReferenceEquals(AxisCycle.Backward.Cycle(Direction.Axis.Z), Direction.Axis.Y),
            "AxisCycle axis mapping must wrap in X/Y/Z order.");

        Require(AxisCycle.None.Inverse() == AxisCycle.None &&
                AxisCycle.Forward.Inverse() == AxisCycle.Backward &&
                AxisCycle.Backward.Inverse() == AxisCycle.Forward,
            "AxisCycle inverse must map each permutation to its inverse.");
        Require(
            AxisCycleExtensions.Between(Direction.Axis.X, Direction.Axis.X) == AxisCycle.None &&
            AxisCycleExtensions.Between(Direction.Axis.X, Direction.Axis.Y) == AxisCycle.Forward &&
            AxisCycleExtensions.Between(Direction.Axis.Y, Direction.Axis.X) == AxisCycle.Backward &&
            AxisCycleExtensions.Between(Direction.Axis.Z, Direction.Axis.Y) == AxisCycle.Backward,
            "AxisCycle.Between must select the positive cyclic difference from source to target.");

        var mutable = new BlockPos.MutableBlockPos();
        mutable.Set(AxisCycle.Forward, 1, 2, 3);
        Require(mutable.Equals(new BlockPos(3, 1, 2)),
            "MutableBlockPos.Set(AxisCycle) must permute X/Y/Z using the source axis selectors.");
    }

    private static void CheckBlockPositionRotation()
    {
        var position = new BlockPos(2, 5, 3);
        Require(ReferenceEquals(position, position.Rotate(Rotation.NONE)),
            "A NONE block-position rotation must return the original instance.");
        Require(position.Rotate(Rotation.CLOCKWISE_90).Equals(new BlockPos(-3, 5, 2)),
            "A clockwise quarter-turn must map (x,y,z) to (-z,y,x).");
        Require(position.Rotate(Rotation.CLOCKWISE_180).Equals(new BlockPos(-2, 5, -3)),
            "A half-turn must negate X and Z while preserving Y.");
        Require(position.Rotate(Rotation.COUNTERCLOCKWISE_90).Equals(new BlockPos(3, 5, -2)),
            "A counter-clockwise quarter-turn must map (x,y,z) to (z,y,-x).");

        var mutable = new BlockPos.MutableBlockPos(2, 5, 3);
        BlockPos rotatedSnapshot = mutable.Rotate(Rotation.CLOCKWISE_90);
        mutable.Move(10, 20, 30);
        Require(rotatedSnapshot.Equals(new BlockPos(-3, 5, 2)) &&
                !ReferenceEquals(rotatedSnapshot, mutable),
            "MutableBlockPos rotation must return an immutable copy unaffected by later mutations.");
    }

    private static void CheckRotationOperations()
    {
        Rotation[] rotations = Rotation.GetValues();
        Require(rotations.Length == 4 &&
                ReferenceEquals(rotations[0], Rotation.NONE) &&
                ReferenceEquals(rotations[1], Rotation.CLOCKWISE_90) &&
                ReferenceEquals(rotations[2], Rotation.CLOCKWISE_180) &&
                ReferenceEquals(rotations[3], Rotation.COUNTERCLOCKWISE_90),
            "Rotation values must preserve the source enum order.");

        Require(
            ReferenceEquals(Rotation.NONE.GetRotation(), OctahedralGroup.IDENTITY) &&
            ReferenceEquals(Rotation.CLOCKWISE_90.GetRotation(), OctahedralGroup.ROT_90_Y_NEG) &&
            ReferenceEquals(Rotation.CLOCKWISE_180.GetRotation(), OctahedralGroup.ROT_180_FACE_XZ) &&
            ReferenceEquals(Rotation.COUNTERCLOCKWISE_90.GetRotation(), OctahedralGroup.ROT_90_Y_POS),
            "Each block rotation must expose its matching octahedral group element.");

        for (int first = 0; first < rotations.Length; first++)
        for (int second = 0; second < rotations.Length; second++)
            Require(
                ReferenceEquals(rotations[first].GetRotated(rotations[second]), rotations[(first + second) % 4]),
                "Rotation composition must add quarter-turn indices modulo four.");

        Require(ReferenceEquals(Rotation.CLOCKWISE_90.Rotate(Direction.NORTH), Direction.EAST) &&
                ReferenceEquals(Rotation.CLOCKWISE_90.Rotate(Direction.EAST), Direction.SOUTH) &&
                ReferenceEquals(Rotation.CLOCKWISE_180.Rotate(Direction.NORTH), Direction.SOUTH) &&
                ReferenceEquals(Rotation.COUNTERCLOCKWISE_90.Rotate(Direction.NORTH), Direction.WEST) &&
                ReferenceEquals(Rotation.COUNTERCLOCKWISE_90.Rotate(Direction.UP), Direction.UP),
            "Rotation direction mapping must rotate horizontal directions and preserve vertical directions.");

        Require(Rotation.CLOCKWISE_90.Rotate(2, 8) == 4 &&
                Rotation.CLOCKWISE_180.Rotate(2, 8) == 6 &&
                Rotation.COUNTERCLOCKWISE_90.Rotate(2, 8) == 0 &&
                Rotation.NONE.Rotate(2, 8) == 2,
            "Rotation integer-step mapping must preserve the source quarter-turn offsets.");

        Require(ReferenceEquals(Rotation.BY_ID(0), Rotation.NONE) &&
                ReferenceEquals(Rotation.BY_ID(-1), Rotation.COUNTERCLOCKWISE_90) &&
                ReferenceEquals(Rotation.BY_ID(4), Rotation.NONE),
            "Rotation BY_ID must wrap IDs using positive modulo.");
    }

    private static void CheckRotationCodecsAndRandom()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Rotation decoded = Rotation.CODEC.Parse(ops, ops.CreateString("clockwise_90")).GetOrThrow();
        Require(ReferenceEquals(decoded, Rotation.CLOCKWISE_90),
            "Rotation.CODEC must decode the serialized rotation key.");
        JsonElement encoded = Rotation.CODEC.EncodeStart(ops, Rotation.CLOCKWISE_180).GetOrThrow();
        Require(ops.GetStringValue(encoded).GetOrThrow() == "180",
            "Rotation.CODEC must encode the source serialized identifier.");

#pragma warning disable CS0618
        Rotation legacy = Rotation.LEGACY_CODEC.Parse(ops, ops.CreateString("COUNTERCLOCKWISE_90")).GetOrThrow();
        JsonElement legacyEncoded = Rotation.LEGACY_CODEC.EncodeStart(ops, Rotation.CLOCKWISE_90).GetOrThrow();
#pragma warning restore CS0618
        Require(ReferenceEquals(legacy, Rotation.COUNTERCLOCKWISE_90) &&
                ops.GetStringValue(legacyEncoded).GetOrThrow() == "CLOCKWISE_90",
            "Rotation.LEGACY_CODEC must use exact enum names for legacy data.");

        var random = new WorldgenRandom(new LegacyRandomSource(1234L));
        _ = Rotation.GetRandom(random);
        List<Rotation> shuffled = Rotation.GetShuffled(random);
        Require(random.Count == 4 && shuffled.Count == 4 &&
                shuffled.Distinct().Count() == 4 &&
                shuffled.All(value => Rotation.GetValues().Any(candidate => ReferenceEquals(value, candidate))),
            "Rotation random helpers must use one bounded draw and the source Fisher-Yates shuffle.");
    }

    private static void CheckMirrorOperations()
    {
        Mirror[] mirrors = Mirror.GetValues();
        Require(mirrors.Length == 3 &&
                ReferenceEquals(mirrors[0], Mirror.NONE) &&
                ReferenceEquals(mirrors[1], Mirror.LEFT_RIGHT) &&
                ReferenceEquals(mirrors[2], Mirror.FRONT_BACK),
            "Mirror values must preserve NONE/LEFT_RIGHT/FRONT_BACK source order.");

        Require(
            ReferenceEquals(Mirror.NONE.GetRotation(), OctahedralGroup.IDENTITY) &&
            ReferenceEquals(Mirror.LEFT_RIGHT.GetRotation(), OctahedralGroup.INVERT_Z) &&
            ReferenceEquals(Mirror.FRONT_BACK.GetRotation(), OctahedralGroup.INVERT_X),
            "Mirror modes must expose the matching octahedral group transform.");

        Require(
            ReferenceEquals(Mirror.LEFT_RIGHT.MirrorDirection(Direction.NORTH), Direction.SOUTH) &&
            ReferenceEquals(Mirror.FRONT_BACK.MirrorDirection(Direction.EAST), Direction.WEST) &&
            ReferenceEquals(Mirror.LEFT_RIGHT.MirrorDirection(Direction.UP), Direction.UP) &&
            ReferenceEquals(Mirror.FRONT_BACK.MirrorDirection(Direction.SOUTH), Direction.SOUTH),
            "Mirror direction transforms must flip only the axis selected by the mirror mode.");

        Require(
            ReferenceEquals(Mirror.LEFT_RIGHT.GetRotation(Direction.NORTH), Rotation.CLOCKWISE_180) &&
            ReferenceEquals(Mirror.LEFT_RIGHT.GetRotation(Direction.EAST), Rotation.NONE) &&
            ReferenceEquals(Mirror.FRONT_BACK.GetRotation(Direction.EAST), Rotation.CLOCKWISE_180) &&
            ReferenceEquals(Mirror.FRONT_BACK.GetRotation(Direction.NORTH), Rotation.NONE),
            "Mirror rotation lookup must return a half-turn only for the matching horizontal axis.");

        Require(
            Mirror.NONE.GetMirrored(3, 8) == 3 &&
            Mirror.LEFT_RIGHT.GetMirrored(1, 8) == 3 &&
            Mirror.LEFT_RIGHT.GetMirrored(7, 8) == 5 &&
            Mirror.FRONT_BACK.GetMirrored(1, 8) == 7 &&
            Mirror.FRONT_BACK.GetMirrored(7, 8) == 1,
            "Mirror index transforms must preserve the source half-step correction and wrap formula.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        Mirror decoded = Mirror.CODEC.Parse(ops, ops.CreateString("left_right")).GetOrThrow();
        JsonElement encoded = Mirror.CODEC.EncodeStart(ops, Mirror.FRONT_BACK).GetOrThrow();
        Require(ReferenceEquals(decoded, Mirror.LEFT_RIGHT) &&
                ops.GetStringValue(encoded).GetOrThrow() == "front_back",
            "Mirror.CODEC must decode and encode source serialized identifiers.");

#pragma warning disable CS0618
        Mirror legacy = Mirror.LEGACY_CODEC.Parse(ops, ops.CreateString("FRONT_BACK")).GetOrThrow();
        JsonElement legacyEncoded = Mirror.LEGACY_CODEC.EncodeStart(ops, Mirror.LEFT_RIGHT).GetOrThrow();
#pragma warning restore CS0618
        Require(ReferenceEquals(legacy, Mirror.FRONT_BACK) &&
                ops.GetStringValue(legacyEncoded).GetOrThrow() == "LEFT_RIGHT",
            "Mirror.LEGACY_CODEC must use exact legacy enum names.");
    }

    private static void CheckBlockStateTransforms()
    {
        BlockState state = Blocks.AIR.DefaultState;
        Require(ReferenceEquals(state, state.Rotate(Rotation.CLOCKWISE_90)),
            "BlockState.Rotate must dispatch through the owning block and preserve the base default state.");
        Require(ReferenceEquals(state, state.Mirror(Mirror.FRONT_BACK)),
            "BlockState.Mirror must dispatch through the owning block and preserve the base default state.");
        Require(!state.IsSignalSource() && !state.HasAnalogOutputSignal(),
            "Base block states must not emit redstone signals or analog output by default.");
        Require(state.CanBeReplaced() == state.IsReplaceable,
            "BlockState.CanBeReplaced must expose the state's configured replacement flag.");
        Require(state.GetDestroySpeed(EmptyBlockGetter.Instance, BlockPos.Zero) == state.DestroySpeed,
            "BlockState.GetDestroySpeed must return the configured destroy speed.");
        Require(state.GetLightDampening() == state.LightDampening,
            "BlockState.GetLightDampening must expose the cached light dampening value.");
        Require(state.GetRenderShape() == RenderShape.MODEL,
            "The default BlockBehaviour render shape must be MODEL.");
        Require(!state.IsEmissiveRendering,
            "Block emissive rendering must be disabled by default.");
        Require(state.GetShadeBrightness(EmptyBlockGetter.Instance, BlockPos.Zero) == 1.0f,
            "Block shade brightness must be one for a non-full collision shape.");
        Require(!state.HasOffsetFunction && state.GetOffset(new BlockPos(10, 20, -20)).Equals(Vec3.Zero),
            "A block state without an offset function must return Vec3.ZERO.");
        Require(Mth.GetSeed(10, 0, -20) == 25548880278638L,
            "Mth.GetSeed must preserve Java's 32-bit coordinate product and signed 64-bit overflow behavior.");

        BlockProperties horizontalOffsetProperties = BlockProperties.Of().OffsetType(OffsetType.XZ);
        Vec3 horizontalOffset = horizontalOffsetProperties.OffsetFunctionValue!(
            state, new BlockPos(10, 99, -20));
        Vec3 sameHorizontalOffset = horizontalOffsetProperties.OffsetFunctionValue!(
            state, new BlockPos(10, -35, -20));
        Require(horizontalOffset.Equals(sameHorizontalOffset) &&
                horizontalOffset.Y == 0.0 &&
                Math.Abs(horizontalOffset.X) <= 0.25 &&
                Math.Abs(horizontalOffset.Z) <= 0.25,
            "XZ block offsets must be deterministic from X/Z and remain within the configured horizontal range.");

        BlockProperties threeAxisOffsetProperties = BlockProperties.Of().OffsetType(OffsetType.XYZ);
        Vec3 threeAxisOffset = threeAxisOffsetProperties.OffsetFunctionValue!(
            state, new BlockPos(10, 99, -20));
        Require(threeAxisOffset.Y <= 0.0 &&
                threeAxisOffset.Y >= -0.2 &&
                threeAxisOffset.X == horizontalOffset.X &&
                threeAxisOffset.Z == horizontalOffset.Z,
            "XYZ block offsets must reuse the XZ offsets and apply the bounded negative vertical offset.");

        Require(BlockProperties.Of().OffsetType(OffsetType.NONE).OffsetFunctionValue is null,
            "NONE block offset type must not install an offset function.");
        Require(!state.ShouldRedstoneWireConnectTo(EmptyBlockGetter.Instance, BlockPos.Zero, null) &&
                !state.ShouldRedstoneWireConnectTo(EmptyBlockGetter.Instance, BlockPos.Zero, Direction.UP),
            "BlockState should not connect redstone wire by default, including when a direction is supplied.");
        Require(state.GetSignal(EmptyBlockGetter.Instance, BlockPos.Zero, Direction.UP) == 0 &&
                state.GetOwnSignal(EmptyBlockGetter.Instance, BlockPos.Zero) == 0 &&
                state.GetDirectSignal(EmptyBlockGetter.Instance, BlockPos.Zero, Direction.UP) == 0,
            "BlockState redstone signal queries must preserve zero-valued base defaults.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
