using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Coordinate slice density function from Minecraft Java 26.3.
/// </summary>
public sealed record SliceFunction(
    Direction.Axis Axis,
    int Coordinate,
    DensityFunction Input) : DensityFunction
{
    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        if (Input is SliceFunction inner)
        {
            if (ReferenceEquals(Axis, Direction.Axis.X) &&
                ReferenceEquals(inner.Axis, Direction.Axis.Z))
            {
                return new XzSampler(inner.Input.CompileSampler(context), Coordinate, inner.Coordinate);
            }

            if (ReferenceEquals(Axis, Direction.Axis.Z) &&
                ReferenceEquals(inner.Axis, Direction.Axis.X))
            {
                return new XzSampler(inner.Input.CompileSampler(context), inner.Coordinate, Coordinate);
            }
        }

        DensitySampler input = Input.CompileSampler(context);
        if (ReferenceEquals(Axis, Direction.Axis.X))
            return new XSampler(input, Coordinate);
        if (ReferenceEquals(Axis, Direction.Axis.Y))
            return new YSampler(input, Coordinate);
        if (ReferenceEquals(Axis, Direction.Axis.Z))
            return new ZSampler(input, Coordinate);

        throw new ArgumentOutOfRangeException(nameof(Axis));
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        DensityFunction input = rule(Input);
        return ReferenceEquals(input, Input)
            ? this
            : new SliceFunction(Axis, Coordinate, input);
    }

    public Interval Range() =>
        Input.Range();

    public int DomainAxes() =>
        Input.DomainAxes() & ~DensityFunction.AxesFrom(Axis);

    public sealed record XzSampler(DensitySampler Input, int X, int Z) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            if (volume.SizeX == 1 &&
                volume.SizeZ == 1 &&
                volume.MinBlockX == X &&
                volume.MinBlockZ == Z)
            {
                Input.SampleVolume(context, outputBuffer, volume);
                return;
            }

            var inputVolume = new DensityVolume(
                1,
                volume.SizeY,
                1,
                X,
                volume.MinBlockY,
                Z,
                volume.StepBlockX,
                volume.StepBlockY,
                volume.StepBlockZ);

            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(inputVolume);
            Input.SampleVolume(context, inputBuffer, inputVolume);

            for (int y = 0; y < volume.SizeY; y++)
            {
                float input = inputBuffer.Get(inputVolume.IndexUnchecked(0, y, 0));
                int outputIndex = volume.IndexUnchecked(0, y, 0);
                for (int z = 0; z < volume.SizeZ; z++)
                {
                    for (int x = 0; x < volume.SizeX; x++)
                    {
                        outputBuffer.Set(outputIndex, input);
                        outputIndex += volume.SizeY;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Input.SampleValue(context, X, blockY, Z);
    }

    public sealed record XSampler(DensitySampler Input, int X) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            if (volume.SizeX == 1 && volume.MinBlockX == X)
            {
                Input.SampleVolume(context, outputBuffer, volume);
                return;
            }

            var inputVolume = new DensityVolume(
                1,
                volume.SizeY,
                volume.SizeZ,
                X,
                volume.MinBlockY,
                volume.MinBlockZ,
                volume.StepBlockX,
                volume.StepBlockY,
                volume.StepBlockZ);

            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(inputVolume);
            Input.SampleVolume(context, inputBuffer, inputVolume);

            int outputIndex = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                for (int x = 0; x < volume.SizeX; x++)
                {
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        outputBuffer.Set(
                            outputIndex,
                            inputBuffer.Get(inputVolume.IndexUnchecked(0, y, z)));
                        outputIndex++;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Input.SampleValue(context, X, blockY, blockZ);
    }

    public sealed record YSampler(DensitySampler Input, int Y) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            if (volume.SizeY == 1 && volume.MinBlockY == Y)
            {
                Input.SampleVolume(context, outputBuffer, volume);
                return;
            }

            var inputVolume = new DensityVolume(
                volume.SizeX,
                1,
                volume.SizeZ,
                volume.MinBlockX,
                Y,
                volume.MinBlockZ,
                volume.StepBlockX,
                volume.StepBlockY,
                volume.StepBlockZ);

            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(inputVolume);
            Input.SampleVolume(context, inputBuffer, inputVolume);

            for (int z = 0; z < volume.SizeZ; z++)
            {
                for (int x = 0; x < volume.SizeX; x++)
                {
                    float input = inputBuffer.Get(inputVolume.IndexUnchecked(x, 0, z));
                    outputBuffer.SetRange(volume.IndexUnchecked(x, 0, z), volume.SizeY, input);
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Input.SampleValue(context, blockX, Y, blockZ);
    }

    public sealed record ZSampler(DensitySampler Input, int Z) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            if (volume.SizeZ == 1 && volume.MinBlockZ == Z)
            {
                Input.SampleVolume(context, outputBuffer, volume);
                return;
            }

            var inputVolume = new DensityVolume(
                volume.SizeX,
                volume.SizeY,
                1,
                volume.MinBlockX,
                volume.MinBlockY,
                Z,
                volume.StepBlockX,
                volume.StepBlockY,
                volume.StepBlockZ);

            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(inputVolume);
            Input.SampleVolume(context, inputBuffer, inputVolume);

            int outputIndex = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                for (int x = 0; x < volume.SizeX; x++)
                {
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        outputBuffer.Set(
                            outputIndex,
                            inputBuffer.Get(inputVolume.IndexUnchecked(x, y, 0)));
                        outputIndex++;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            Input.SampleValue(context, blockX, blockY, Z);
    }
}
