using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.DensityFunctions.Generator;

/// <summary>
/// Context-provided density functions from Minecraft Java 26.3.
/// </summary>
public sealed class SimpleDensityFunction : DensityFunction
{
    public static readonly SimpleDensityFunction BLEND_ALPHA = new("blend_alpha");
    public static readonly SimpleDensityFunction BLEND_OFFSET = new("blend_offset");
    public static readonly SimpleDensityFunction BEARDIFIER = new("beardifier");

    public static IReadOnlyList<SimpleDensityFunction> Values { get; } =
    [
        BLEND_ALPHA,
        BLEND_OFFSET,
        BEARDIFIER
    ];

    public string Id { get; }

    private SimpleDensityFunction(string id) =>
        Id = id;

    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        this switch
        {
            var function when ReferenceEquals(function, BLEND_ALPHA) =>
                new ContextBoundSampler(
                    DensityFunctionContextKeys.BLEND_ALPHA,
                    new ConstantFunction.Sampler(1.0f)),
            var function when ReferenceEquals(function, BLEND_OFFSET) =>
                new ContextBoundSampler(
                    DensityFunctionContextKeys.BLEND_OFFSET,
                    new ConstantFunction.Sampler(0.0f)),
            var function when ReferenceEquals(function, BEARDIFIER) =>
                new ContextBoundSampler(
                    DensityFunctionContextKeys.BEARDIFIER,
                    new ConstantFunction.Sampler(0.0f)),
            _ => throw new InvalidOperationException($"Unknown simple density function: {Id}")
        };

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return this;
    }

    public Interval Range() =>
        ReferenceEquals(this, BLEND_ALPHA)
            ? Interval.Of(0.0f, 1.0f)
            : Interval.INFINITE;

    public int DomainAxes() =>
        ReferenceEquals(this, BEARDIFIER)
            ? DensityFunction.ALL_AXES
            : DensityFunction.AXIS_X | DensityFunction.AXIS_Z;

    public override string ToString() => Id;
}
