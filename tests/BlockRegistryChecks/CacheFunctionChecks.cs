using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class CacheFunctionChecks
{
    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("CacheFunction checks must not request noise.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("CacheFunction checks must not request random streams.");

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("CacheFunction checks must not request End Island random streams.");
#pragma warning restore CS0618
    }

    public static void Run()
    {
        CheckFactoryAndStaticMetadata();
        CheckRewriteIdentityAndReplacement();
        CheckDirectCompilationIsRejected();
    }

    private static void CheckFactoryAndStaticMetadata()
    {
        DensityFunction input = DensityFunctions.Clamp(
            DensityFunctions.Constant(2.5f),
            -1.0f,
            1.0f);
        DensityFunction cache = DensityFunctions.Cache(input);

        Require(
            cache is CacheFunction cacheFunction &&
            ReferenceEquals(cacheFunction.Input, input),
            "DensityFunctions.Cache must create a CacheFunction around the exact input node.");

        Require(
            cache.Range().Equals(input.Range()) &&
            cache.DomainAxes() == input.DomainAxes(),
            "CacheFunction must expose its input range and domain axes unchanged.");
    }

    private static void CheckRewriteIdentityAndReplacement()
    {
        DensityFunction input = DensityFunctions.Constant(4.0f);
        var cache = new CacheFunction(input);

        DensityFunction unchanged = cache.RewriteChildren(function => function);
        Require(
            ReferenceEquals(unchanged, cache),
            "CacheFunction.rewriteChildren must preserve identity when its input is unchanged.");

        DensityFunction replacement = DensityFunctions.Constant(-6.0f);
        DensityFunction rewritten = cache.RewriteChildren(
            function => ReferenceEquals(function, input) ? replacement : function);

        Require(
            rewritten is CacheFunction replacedCache &&
            ReferenceEquals(replacedCache.Input, replacement) &&
            !ReferenceEquals(rewritten, cache),
            "CacheFunction.rewriteChildren must rebuild the wrapper when the input changes.");
    }

    private static void CheckDirectCompilationIsRejected()
    {
        var cache = new CacheFunction(DensityFunctions.Constant(1.0f));
        try
        {
            _ = cache.CompileSampler(new UnusedCompileContext());
        }
        catch (NotSupportedException)
        {
            return;
        }

        throw new InvalidOperationException(
            "CacheFunction.compileSampler must reject direct compilation before DensityFunctionCompiler rewrites it.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
