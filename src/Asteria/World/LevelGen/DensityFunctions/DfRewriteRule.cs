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
