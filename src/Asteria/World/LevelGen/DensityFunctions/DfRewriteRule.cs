namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Functional rewrite operation for density-function graph nodes.
/// </summary>
public delegate DensityFunction DfRewriteRule(DensityFunction function);

/// <summary>
/// Static composition helpers for density-function rewrite rules.
/// </summary>
public static class DfRewriteRules
{
    /// <summary>
    /// Inlines holder-backed density-function nodes, matching Minecraft Java 26.3.
    /// </summary>
    public static DfRewriteRule InlineReference { get; } =
        function => function is DensityFunctions.HolderHolder holder
            ? holder.Function.Value
            : function;

    /// <summary>
    /// Removes every coordinate axis unused by a density-function subtree.
    /// </summary>
    public static DfRewriteRule SliceUniformAxes { get; } =
        new SliceUniformAxesRewriteRule(DensityFunction.ALL_AXES).Rewrite;

    public static DfRewriteRule Sequence(params DfRewriteRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        DfRewriteRule[] orderedRules = (DfRewriteRule[])rules.Clone();

        return function =>
        {
            foreach (DfRewriteRule rule in orderedRules)
                function = rule(function);

            return function;
        };
    }
}
