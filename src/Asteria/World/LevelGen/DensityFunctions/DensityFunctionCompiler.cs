using System.Collections.Concurrent;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions.Operations;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Rewrites and compiles density-function graphs once per compiler, matching
/// Minecraft Java 26.3 DensityFunctionCompiler.
/// </summary>
public sealed class DensityFunctionCompiler
{
    private readonly DensityFunction.CompileContext _context;
    private readonly ConcurrentDictionary<DensityFunction, Lazy<DensitySampler>> _samplers = new();
    private readonly Dictionary<DensityFunction, PreparedCache> _preparedCaches = new();
    private readonly object _compileLock = new();
    private readonly DfRewriteRule _optimizerRule;
    private int _nextCacheId;

    public DensityFunctionCompiler(DensityFunction.CompileContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _optimizerRule = DfRewriteRules.Sequence(
            OptimizeDensityFunction,
            DfRewriteRules.SliceUniformAxes);
    }

    /// <summary>
    /// Gets the compiled sampler for a graph, memoizing equal density functions.
    /// Lazy publication ensures the same key is optimized and compiled only once.
    /// </summary>
    public DensitySampler GetSampler(DensityFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        Lazy<DensitySampler> sampler = _samplers.GetOrAdd(
            function,
            densityFunction => new Lazy<DensitySampler>(
                () => OptimizeAndCompile(densityFunction),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return sampler.Value;
    }

    private DensitySampler OptimizeAndCompile(DensityFunction function)
    {
        lock (_compileLock)
        {
            DensityFunction optimizedFunction = _optimizerRule(function);
            return optimizedFunction.CompileSampler(_context);
        }
    }

    private DensityFunction OptimizeDensityFunction(DensityFunction function)
    {
        function = DfRewriteRules.InlineReference(function);

        return function is CacheFunction cache
            ? ReuseOrPrepareCache(cache)
            : function.RewriteChildren(OptimizeDensityFunction);
    }

    private DensityFunction ReuseOrPrepareCache(CacheFunction cache)
    {
        if (_preparedCaches.TryGetValue(cache.Input, out PreparedCache? prepared))
            return prepared;

        prepared = PrepareCache(cache);
        _preparedCaches.Add(cache.Input, prepared);
        return prepared;
    }

    private PreparedCache PrepareCache(CacheFunction cache)
    {
        int id = _nextCacheId++;
        DensityFunction input = _optimizerRule(cache.Input);
        var cachingSampler = new CachingDensitySampler(
            id,
            input.CompileSampler(_context));

        return new PreparedCache(
            id,
            input.Range(),
            input.DomainAxes(),
            cachingSampler);
    }

    private sealed record PreparedCache(
        int Id,
        Interval ValueRange,
        int Axes,
        DensitySampler CachingSampler) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            CachingSampler;

        public DensityFunction RewriteChildren(DfRewriteRule rule) =>
            this;

        public Interval Range() =>
            ValueRange;

        public int DomainAxes() =>
            Axes;
    }
}
