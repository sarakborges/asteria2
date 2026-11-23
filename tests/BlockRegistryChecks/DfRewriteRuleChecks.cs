using Asteria.Core;
using Asteria.Content.Registries;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class DfRewriteRuleChecks
{
    public static void Run()
    {
        CheckInlineReference();
        CheckSkippedFunctions();
        CheckRecursiveAxisSlicing();
        CheckParentDomainAxesEquality();
        CheckExistingSliceAxes();
        CheckAxisInsertionOrder();
    }

    private static void CheckInlineReference()
    {
        DensityFunction referenced = DensityFunctions.Constant(4.25f);
        var holderFunction = new DensityFunctions.HolderHolder(
            Holder<DensityFunction>.Direct(referenced));

        Require(
            ReferenceEquals(DfRewriteRules.InlineReference(holderFunction), referenced),
            "INLINE_REFERENCE must replace a HolderHolder with its holder value.");

        DensityFunction unrelated = DensityFunctions.Constant(-2.0f);
        Require(
            ReferenceEquals(DfRewriteRules.InlineReference(unrelated), unrelated),
            "INLINE_REFERENCE must preserve every non-HolderHolder density function.");
    }

    private static void CheckSkippedFunctions()
    {
        DensityFunction constant = DensityFunctions.Constant(3.0f);
        DensityFunction gradient = CreateGradient(Direction.Axis.X);

        Require(
            ReferenceEquals(DfRewriteRules.SliceUniformAxes(constant), constant),
            "SLICE_UNIFORM_AXES must not wrap ConstantFunction.");

        Require(
            ReferenceEquals(DfRewriteRules.SliceUniformAxes(gradient), gradient),
            "SLICE_UNIFORM_AXES must not wrap GradientFunction.");
    }

    private static void CheckRecursiveAxisSlicing()
    {
        DensityFunction xGradient = CreateGradient(Direction.Axis.X);
        DensityFunction zGradient = CreateGradient(Direction.Axis.Z);
        DensityFunction xNode = DensityFunctions.Clamp(xGradient, -0.5f, 0.5f);
        DensityFunction zNode = DensityFunctions.Clamp(zGradient, -0.5f, 0.5f);
        DensityFunction combined = DensityFunctions.Add(xNode, zNode);

        DensityFunction rewritten = DfRewriteRules.SliceUniformAxes(combined);
        SliceFunction ySlice = RequireType<SliceFunction>(rewritten);
        Require(
            ReferenceEquals(ySlice.Axis, Direction.Axis.Y) && ySlice.Coordinate == 0,
            "The root subtree must be sliced on Y when its combined graph does not depend on Y.");

        BinaryFunction binary = RequireType<BinaryFunction>(ySlice.Input);
        SliceFunction leftZSlice = RequireType<SliceFunction>(binary.Left);
        SliceFunction rightXSlice = RequireType<SliceFunction>(binary.Right);

        Require(
            ReferenceEquals(leftZSlice.Axis, Direction.Axis.Z) &&
            leftZSlice.Coordinate == 0 &&
            ReferenceEquals(leftZSlice.Input, xNode) &&
            ReferenceEquals(RequireType<ClampFunction>(leftZSlice.Input).Input, xGradient),
            "The X-gradient branch must be sliced only on its unused Z axis.");

        Require(
            ReferenceEquals(rightXSlice.Axis, Direction.Axis.X) &&
            rightXSlice.Coordinate == 0 &&
            ReferenceEquals(rightXSlice.Input, zNode) &&
            ReferenceEquals(RequireType<ClampFunction>(rightXSlice.Input).Input, zGradient),
            "The Z-gradient branch must be sliced only on its unused X axis.");

        Require(
            rewritten.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "Uniform-axis rewriting must preserve the original graph's effective domain axes.");
    }

    private static void CheckParentDomainAxesEquality()
    {
        DensityFunction xGradient = CreateGradient(Direction.Axis.X);
        DensityFunction zGradient = CreateGradient(Direction.Axis.Z);
        DensityFunction xNode = DensityFunctions.Clamp(xGradient, -0.5f, 0.5f);
        DensityFunction zNode = DensityFunctions.Clamp(zGradient, -0.5f, 0.5f);
        DensityFunction combined = DensityFunctions.Add(xNode, zNode);
        var rule = new SliceUniformAxesRewriteRule(
            DensityFunction.AXIS_X | DensityFunction.AXIS_Z);

        DensityFunction rewritten = rule.Rewrite(combined);
        BinaryFunction binary = RequireType<BinaryFunction>(rewritten);

        Require(
            binary.Left is SliceFunction left &&
            ReferenceEquals(left.Axis, Direction.Axis.Z) &&
            ReferenceEquals(left.Input, xNode) &&
            ReferenceEquals(RequireType<ClampFunction>(left.Input).Input, xGradient),
            "When parent axes equal the current node axes, the same rule must recursively rewrite the left child.");

        Require(
            binary.Right is SliceFunction right &&
            ReferenceEquals(right.Axis, Direction.Axis.X) &&
            ReferenceEquals(right.Input, zNode) &&
            ReferenceEquals(RequireType<ClampFunction>(right.Input).Input, zGradient),
            "When parent axes equal the current node axes, the same rule must recursively rewrite the right child.");

        Require(
            rewritten.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z),
            "The parent-domain equality path must not introduce a slice on an axis used by the parent.");
    }

    private static void CheckExistingSliceAxes()
    {
        DensityFunction xGradient = CreateGradient(Direction.Axis.X);
        DensityFunction existingZSlice = new SliceFunction(
            Direction.Axis.Z,
            19,
            xGradient);

        DensityFunction rewritten = DfRewriteRules.SliceUniformAxes(existingZSlice);
        SliceFunction addedYSlice = RequireType<SliceFunction>(rewritten);

        Require(
            ReferenceEquals(addedYSlice.Axis, Direction.Axis.Y) &&
            addedYSlice.Coordinate == 0 &&
            ReferenceEquals(addedYSlice.Input, existingZSlice),
            "The rewrite must add Y while retaining the existing Z slice without wrapping Z a second time.");

        Require(
            rewritten.DomainAxes() == DensityFunction.AXIS_X,
            "Removing already-sliced axes must preserve the remaining X domain.");
    }

    private static void CheckAxisInsertionOrder()
    {
        DensityFunction binary = DensityFunctions.Add(
            DensityFunctions.Constant(2.0f),
            DensityFunctions.Constant(5.0f));

        DensityFunction rewritten = DfRewriteRules.SliceUniformAxes(binary);
        SliceFunction ySlice = RequireType<SliceFunction>(rewritten);
        SliceFunction zSlice = RequireType<SliceFunction>(ySlice.Input);
        SliceFunction xSlice = RequireType<SliceFunction>(zSlice.Input);

        Require(
            ReferenceEquals(ySlice.Axis, Direction.Axis.Y) &&
            ReferenceEquals(zSlice.Axis, Direction.Axis.Z) &&
            ReferenceEquals(xSlice.Axis, Direction.Axis.X) &&
            ReferenceEquals(xSlice.Input, binary),
            "Missing axes must be wrapped in the reference X, Z, Y insertion order.");

        Require(
            rewritten.DomainAxes() == DensityFunction.NO_AXES,
            "Slicing all unused axes must leave the constant-only subtree coordinate independent.");
    }

    private static GradientFunction CreateGradient(Direction.Axis axis) =>
        new(
            axis,
            TilingMode.CLAMP_TO_EDGE,
            -16,
            16,
            -1.0f,
            1.0f);

    private static T RequireType<T>(DensityFunction function) where T : DensityFunction =>
        function is T typed
            ? typed
            : throw new InvalidOperationException(
                $"Expected density function type {typeof(T).Name}, got {function.GetType().Name}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
