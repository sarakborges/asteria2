using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Density-function cubic spline from Minecraft Java 26.3.
/// The stored spline uses uncompiled coordinate descriptors; compilation maps them
/// to sampler-backed coordinates and assigns one scratch-buffer slot per occurrence.
/// </summary>
public sealed class SplineFunction : DensityFunction
{
    private static Codec<IBoundedFloatFunction<Unit>> CoordinateCodec { get; } =
        DensityFunction.CODEC.XMap<IBoundedFloatFunction<Unit>>(
            static function => new Coordinate(function),
            static coordinate => ((Coordinate)coordinate).Function);

    public static MapCodec<SplineFunction> CODEC { get; } =
        CubicSpline<Unit>.CreateCodec(CoordinateCodec)
            .FieldOf("spline")
            .XMap<SplineFunction>(
                static spline => new SplineFunction(spline),
                static function => function.Spline);

    public CubicSpline<Unit> Spline { get; }

    public SplineFunction(CubicSpline<Unit> spline)
    {
        Spline = spline ?? throw new ArgumentNullException(nameof(spline));
        Spline.ForEachCoordinate(static coordinate =>
        {
            if (coordinate is not Coordinate)
            {
                throw new ArgumentException(
                    $"Spline coordinate must be {nameof(Coordinate)}, got {coordinate.GetType().Name}.",
                    nameof(spline));
            }
        });
    }

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var samplerCoordinates = new List<SamplerCoordinate>();
        Spline.ForEachCoordinate(coordinate =>
        {
            Coordinate descriptor = GetCoordinate(coordinate);
            DensitySampler sampler = descriptor.Function.CompileSampler(context);
            samplerCoordinates.Add(new SamplerCoordinate(
                sampler,
                samplerCoordinates.Count,
                descriptor.Range()));
        });

        int nextCoordinate = 0;
        CubicSpline<SplineInput> compiledSpline = Spline.MapCoordinates<SplineInput>(coordinate =>
        {
            if (nextCoordinate >= samplerCoordinates.Count)
                throw new InvalidOperationException("Cubic spline coordinate traversal changed during compilation.");

            return samplerCoordinates[nextCoordinate++];
        });

        if (nextCoordinate != samplerCoordinates.Count)
        {
            throw new InvalidOperationException(
                $"Cubic spline coordinate count changed during compilation: expected {samplerCoordinates.Count}, mapped {nextCoordinate}.");
        }

        return new Sampler(compiledSpline, samplerCoordinates.Count);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        bool changed = false;

        CubicSpline<Unit> rewrittenSpline = Spline.MapCoordinates<Unit>(coordinate =>
        {
            Coordinate descriptor = GetCoordinate(coordinate);
            DensityFunction rewritten = rule(descriptor.Function);
            if (ReferenceEquals(rewritten, descriptor.Function))
                return descriptor;

            changed = true;
            return new Coordinate(rewritten);
        });

        return changed ? new SplineFunction(rewrittenSpline) : this;
    }

    public Interval Range() =>
        Spline.Range();

    public int DomainAxes()
    {
        int axes = DensityFunction.NO_AXES;
        Spline.ForEachCoordinate(coordinate =>
        {
            axes |= GetCoordinate(coordinate).Function.DomainAxes();
        });
        return axes;
    }

    private static Coordinate GetCoordinate(IBoundedFloatFunction<Unit> coordinate) =>
        coordinate as Coordinate ??
        throw new InvalidOperationException(
            $"Expected {nameof(Coordinate)} in a {nameof(SplineFunction)} graph, got {coordinate.GetType().Name}.");

    /// <summary>
    /// Marker context for the serialized, uncompiled coordinate descriptors.
    /// </summary>
    public readonly record struct Unit;

    /// <summary>
    /// A density function embedded as a cubic-spline coordinate before compilation.
    /// </summary>
    public sealed record Coordinate : IBoundedFloatFunction<Unit>
    {
        public DensityFunction Function { get; }

        public Coordinate(DensityFunction function) =>
            Function = function ?? throw new ArgumentNullException(nameof(function));

        public float Apply(Unit context) =>
            throw new InvalidOperationException(
                "An uncompiled spline coordinate cannot be sampled; compile the enclosing SplineFunction first.");

        public Interval Range() =>
            Function.Range();
    }

    /// <summary>
    /// Evaluation contract shared by point and volume spline inputs.
    /// </summary>
    public interface SplineInput
    {
        float SampleCoordinate(DensitySampler coordinateSampler, int coordinateIndex);
    }

    private sealed record SamplerCoordinate(
        DensitySampler Sampler,
        int Index,
        Interval ValueRange) : IBoundedFloatFunction<SplineInput>
    {
        public float Apply(SplineInput context) =>
            context.SampleCoordinate(Sampler, Index);

        public Interval Range() =>
            ValueRange;
    }

    private sealed class PointSplineInput : SplineInput
    {
        private readonly SamplerContext _context;
        private readonly int _blockX;
        private readonly int _blockY;
        private readonly int _blockZ;
        private readonly float[] _cachedValues;

        public PointSplineInput(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ,
            int coordinateCount)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _blockX = blockX;
            _blockY = blockY;
            _blockZ = blockZ;
            _cachedValues = new float[coordinateCount];
            Array.Fill(_cachedValues, float.NaN);
        }

        public float SampleCoordinate(DensitySampler coordinateSampler, int coordinateIndex)
        {
            float cached = _cachedValues[coordinateIndex];
            if (!float.IsNaN(cached))
                return cached;

            float sampled = coordinateSampler.SampleValue(
                _context,
                _blockX,
                _blockY,
                _blockZ);
            _cachedValues[coordinateIndex] = sampled;
            return sampled;
        }
    }

    private sealed class BufferSplineInput : SplineInput, IDisposable
    {
        private readonly SamplerContext _context;
        private readonly DensityVolume _volume;
        private readonly ScopedDensityBuffer?[] _buffers;

        public int Index { get; set; }

        public BufferSplineInput(
            SamplerContext context,
            DensityVolume volume,
            int coordinateCount)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _volume = volume ?? throw new ArgumentNullException(nameof(volume));
            _buffers = new ScopedDensityBuffer?[coordinateCount];
        }

        public float SampleCoordinate(DensitySampler coordinateSampler, int coordinateIndex)
        {
            ArgumentNullException.ThrowIfNull(coordinateSampler);

            ScopedDensityBuffer? buffer = _buffers[coordinateIndex];
            if (buffer is null)
            {
                buffer = _context.AcquireBuffer(_volume);
                _buffers[coordinateIndex] = buffer;
                coordinateSampler.SampleVolume(_context, buffer, _volume);
            }

            return buffer.Get(Index);
        }

        public void Dispose()
        {
            foreach (ScopedDensityBuffer? buffer in _buffers)
                buffer?.Close();
        }
    }

    private sealed record Sampler(
        CubicSpline<SplineInput> Spline,
        int CoordinateCount) : DensitySampler
    {
        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ)
        {
            var input = new PointSplineInput(
                context,
                blockX,
                blockY,
                blockZ,
                CoordinateCount);

            return CubicSpline<SplineInput>.Sample(Spline, input);
        }

        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            using var input = new BufferSplineInput(context, volume, CoordinateCount);
            int size = volume.Size();

            for (int index = 0; index < size; index++)
            {
                input.Index = index;
                outputBuffer.Set(
                    index,
                    CubicSpline<SplineInput>.Sample(Spline, input));
            }
        }
    }
}
