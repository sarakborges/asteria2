using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Cache request density function from Minecraft Java 26.3.
/// It must be replaced by DensityFunctionCompiler before sampler compilation.
/// </summary>
public sealed record CacheFunction(DensityFunction Input) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        throw new NotSupportedException(
            "CacheFunction must be rewritten by DensityFunctionCompiler before compilation.");

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        return ReferenceEquals(input, Input)
            ? this
            : new CacheFunction(input);
    }

    public Interval Range() =>
        Input.Range();

    public int DomainAxes() =>
        Input.DomainAxes();
}
