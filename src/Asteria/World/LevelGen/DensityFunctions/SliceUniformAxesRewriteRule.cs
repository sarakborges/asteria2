using Asteria.Core;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Recursive uniform-axis slicing rewrite from Minecraft Java 26.3.
/// </summary>
public sealed record SliceUniformAxesRewriteRule(int ParentDomainAxes)
{
    public DensityFunction Rewrite(DensityFunction function)
    {
        if (function is ConstantFunction or GradientFunction)
            return function;

        int domainAxes = function.DomainAxes();
        if (ParentDomainAxes == domainAxes)
            return function.RewriteChildren(Rewrite);

        DensityFunction rewritten = function.RewriteChildren(
            new SliceUniformAxesRewriteRule(domainAxes).Rewrite);
        int removedAxes = ParentDomainAxes & ~domainAxes;
        return RemoveAxes(rewritten, removedAxes);
    }

    private static DensityFunction RemoveAxes(DensityFunction function, int axes)
    {
        int filteredAxes = axes & ~GetExistingRemovedAxes(function);

        if ((filteredAxes & DensityFunction.AXIS_X) != 0)
            function = new SliceFunction(Direction.Axis.X, 0, function);

        if ((filteredAxes & DensityFunction.AXIS_Z) != 0)
            function = new SliceFunction(Direction.Axis.Z, 0, function);

        if ((filteredAxes & DensityFunction.AXIS_Y) != 0)
            function = new SliceFunction(Direction.Axis.Y, 0, function);

        return function;
    }

    private static int GetExistingRemovedAxes(DensityFunction function)
    {
        int axes = DensityFunction.NO_AXES;

        while (function is SliceFunction slice)
        {
            axes |= DensityFunction.AxesFrom(slice.Axis);
            function = slice.Input;
        }

        return axes;
    }
}
