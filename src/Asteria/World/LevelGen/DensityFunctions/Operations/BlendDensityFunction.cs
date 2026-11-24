using Asteria.Serialization;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Density wrapper that delegates old-terrain blending to the chunk blender in
/// the sampling context, matching Minecraft Java 26.3 BlendDensityFunction.
/// </summary>
public sealed record BlendDensityFunction(DensityFunction Input) : DensityFunction
{
    public static MapCodec<BlendDensityFunction> CODEC { get; } =
        DensityFunction.CODEC.FieldOf("input")
            .XMap<BlendDensityFunction>(
                static input => new BlendDensityFunction(input),
                static function => function.Input);

    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        new Sampler(Input.CompileSampler(context));

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        DensityFunction input = rule(Input);
        return ReferenceEquals(input, Input)
            ? this
            : new BlendDensityFunction(input);
    }

    public Interval Range() =>
        Input.Range();

    public int DomainAxes() =>
        Input.DomainAxes();

    public sealed record Sampler(DensitySampler Input) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(outputBuffer);
            ArgumentNullException.ThrowIfNull(volume);

            Input.SampleVolume(context, outputBuffer, volume);

            IChunkBlender? blender = context.GetField(DensityFunctionContextKeys.BLENDER);
            if (blender is null || blender.IsEmpty)
                return;

            for (int z = 0; z < volume.SizeZ; z++)
            {
                int blockZ = volume.BlockZ(z);
                for (int x = 0; x < volume.SizeX; x++)
                {
                    int blockX = volume.BlockX(x);
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        int index = volume.IndexUnchecked(x, y, z);
                        outputBuffer.Set(
                            index,
                            blender.BlendDensity(
                                blockX,
                                volume.BlockY(y),
                                blockZ,
                                outputBuffer.Get(index)));
                    }
                }
            }
        }

        public float SampleValue(
            SamplerContext context,
            int blockX,
            int blockY,
            int blockZ)
        {
            float density = Input.SampleValue(context, blockX, blockY, blockZ);
            IChunkBlender? blender = context.GetField(DensityFunctionContextKeys.BLENDER);

            return blender is null || blender.IsEmpty
                ? density
                : blender.BlendDensity(blockX, blockY, blockZ, density);
        }
    }
}
