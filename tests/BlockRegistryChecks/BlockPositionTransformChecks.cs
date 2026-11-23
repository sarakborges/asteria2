using System.Text.Json;
using Asteria.Content.Blocks;
using Asteria.Content.Blocks.StateProperties;
using Asteria.Content.Materials;
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
        CheckMapColor();
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
        Require(!state.HasOffsetFunction && state.GetOffset(new BlockPos(10, 20, -20)).Equals(Vec3.ZERO),
            "A block state without an offset function must return Vec3.ZERO.");
        Require(!state.IsSuffocating(EmptyBlockGetter.Instance, BlockPos.Zero) &&
                !state.IsViewBlocking(EmptyBlockGetter.Instance, BlockPos.Zero, new AABB(0, 0, 0, 1, 1, 1)),
            "Default suffocation and view-blocking predicates must use the causes-suffocation tag.");
        Require(state.GetPostProcessPos(EmptyBlockGetter.Instance, BlockPos.Zero) is null,
            "The default post-process predicate must return no redirected position.");
        Require(state.PistonPushReaction == PushReaction.PUSH_PULL &&
                state.GetPistonPushReaction() == PushReaction.PUSH_PULL,
            "Block states must use PUSH_PULL as the default piston reaction and expose its source getter.");
        Require(BlockProperties.Of().PushReaction(PushReaction.IGNORE_ENTITY).PushReactionValue == PushReaction.IGNORE_ENTITY,
            "Block properties must retain a configured piston push reaction.");

        bool customSuffocatingPredicate = BlockProperties.Of()
            .IsSuffocating(static (_, _, _) => true)
            .IsSuffocatingValue(state, EmptyBlockGetter.Instance, BlockPos.Zero);
        bool customViewBlockingPredicate = BlockProperties.Of()
            .IsViewBlocking(static (_, _, _, box) => box.MaxX > box.MinX)
            .IsViewBlockingValue(state, EmptyBlockGetter.Instance, BlockPos.Zero, new AABB(0, 0, 0, 1, 1, 1));
        BlockPos redirectedPosition = new BlockPos(3, 4, 5);
        BlockPos? customPostProcessPosition = BlockProperties.Of()
            .PostProcess((_, _, _) => redirectedPosition)
            .PostProcessValue(state, EmptyBlockGetter.Instance, BlockPos.Zero);
        Require(customSuffocatingPredicate && customViewBlockingPredicate &&
                ReferenceEquals(customPostProcessPosition, redirectedPosition),
            "Block properties must preserve custom state predicate and post-process delegates.");
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
    }

    private sealed class MapColorTestBlock : Block
    {
        public MapColorTestBlock(BlockProperties properties, BooleanProperty variant)
            : base(
                properties,
                createIntrusiveRegistryHolder: false,
                configureState: builder => builder.Add(variant))
        {
            InitializeStateCaches();
        }

        public void SetDefaultStateForTest(BlockState state) =>
            RegisterDefaultState(state);
    }

    private static void CheckMapColor()
    {
        Require(MapColor.ById(0) == MapColor.NONE &&
                MapColor.ById(1) == MapColor.GRASS &&
                MapColor.ById(61) == MapColor.GLOW_LICHEN &&
                MapColor.ById(62) == MapColor.NONE &&
                MapColor.ById(63) == MapColor.NONE,
            "Map-color ID lookup must preserve the 26.3 palette, including unregistered trailing slots.");
        Require(MapColor.GRASS.Col == 8368696 &&
                MapColor.TERRACOTTA_LIGHT_BLUE.Id == 39 &&
                ReferenceEquals(MapColor.Brightness.ById(0), MapColor.Brightness.LOW) &&
                ReferenceEquals(MapColor.Brightness.ById(1), MapColor.Brightness.NORMAL) &&
                ReferenceEquals(MapColor.Brightness.ById(2), MapColor.Brightness.HIGH) &&
                ReferenceEquals(MapColor.Brightness.ById(3), MapColor.Brightness.LOWEST),
            "Map-color RGB values, IDs and brightness ordinal order must match 26.3.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => MapColor.ById(-1),
            "Map-color lookup must reject negative IDs.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => MapColor.ById(65),
            "Map-color lookup must reject IDs above the source array's length.");
        RequireThrows<IndexOutOfRangeException>(
            () => MapColor.ById(64),
            "Map-color lookup must preserve the source check-position-index boundary at id == length.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => MapColor.Brightness.ById(-1),
            "Brightness lookup must reject negative IDs.");
        RequireThrows<IndexOutOfRangeException>(
            () => MapColor.Brightness.ById(4),
            "Brightness lookup must preserve the source array boundary at id == length.");

        Require(MapColor.NONE.CalculateArgbColor(MapColor.Brightness.HIGH) == 0 &&
                unchecked((uint)MapColor.GRASS.CalculateArgbColor(MapColor.Brightness.HIGH)) == 0xFF7FB238U &&
                unchecked((uint)MapColor.GRASS.CalculateArgbColor(MapColor.Brightness.NORMAL)) == 0xFF6D9930U &&
                unchecked((uint)MapColor.GRASS.CalculateArgbColor(MapColor.Brightness.LOW)) == 0xFF597D27U &&
                unchecked((uint)MapColor.GRASS.CalculateArgbColor(MapColor.Brightness.LOWEST)) == 0xFF435E1DU,
            "Map-color brightness must preserve opaque ARGB channels and integer scaling/truncation.");
        byte packed = MapColor.GRASS.GetPackedId(MapColor.Brightness.NORMAL);
        Require(packed == 5 &&
                unchecked((uint)MapColor.GetColorFromPackedId(packed)) == 0xFF6D9930U &&
                MapColor.GetColorFromPackedId(-1) == 0,
            "Packed map-color IDs must encode the palette ID and brightness in the low eight bits.");

        BooleanProperty variant = BooleanProperty.Create("map_color_variant");
        MapColorTestBlock block = new(
            BlockProperties.Of().MapColor(state =>
                state.GetValue(variant) ? MapColor.GRASS : MapColor.SAND),
            variant);
        BlockState grassState = block.DefaultState.SetValue(variant, true);
        BlockState sandState = block.DefaultState.SetValue(variant, false);
        Require(ReferenceEquals(grassState.GetMapColor(EmptyBlockGetter.Instance, BlockPos.Zero), MapColor.GRASS) &&
                ReferenceEquals(sandState.GetMapColor(EmptyBlockGetter.Instance, BlockPos.Zero), MapColor.SAND),
            "Map color must be evaluated for and snapshotted by each immutable block state.");
        block.SetDefaultStateForTest(sandState);
        Require(ReferenceEquals(block.GetDefaultMapColor(), MapColor.SAND),
            "Block default map color must evaluate the map-color property against its current default state.");

        MapColorTestBlock fixedBlock = new(
            BlockProperties.Of().MapColor(MapColor.WOOD),
            BooleanProperty.Create("fixed_map_color_variant"));
        Require(ReferenceEquals(
                fixedBlock.DefaultState.GetMapColor(EmptyBlockGetter.Instance, BlockPos.Zero),
                MapColor.WOOD),
            "The fixed map-color property overload must apply the same palette entry to every state.");
        Require(ReferenceEquals(
                Blocks.STONE.DefaultState.GetMapColor(EmptyBlockGetter.Instance, BlockPos.Zero),
                MapColor.NONE),
            "Block map colors must default to MapColor.NONE.");
        RequireThrows<InvalidOperationException>(
            () => new MapColorTestBlock(
                BlockProperties.Of().MapColor(static _ => null!),
                BooleanProperty.Create("null_map_color_variant")),
            "A block state must reject a null map-color callback result during construction.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
