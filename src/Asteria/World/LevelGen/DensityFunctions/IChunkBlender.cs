using Asteria.Core;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Chunk-scoped terrain blender consumed by Minecraft Java 26.3 blend_density and the
/// blend_alpha/blend_offset simple density functions.
/// </summary>
public interface IChunkBlender
{
    static IChunkBlender Empty { get; } = new EmptyChunkBlender();

    bool IsEmpty { get; }

    float BlendDensity(int blockX, int blockY, int blockZ, float density);

    BlenderOutputBuffer BlendOffsetAndFactor(DensityVolume volume);
}

/// <summary>
/// Per-volume quart-grid blend alpha and vertical offset, matching Blender.OutputBuffer.
/// </summary>
public sealed class BlenderOutputBuffer
{
    private readonly float[] _alphas;
    private readonly float[] _offsets;

    public int MinCellX { get; }
    public int MinCellZ { get; }
    public int CellCountX { get; }
    public int CellCountZ { get; }

    public BlenderOutputBuffer(
        int minCellX,
        int minCellZ,
        int cellCountX,
        int cellCountZ,
        float[] alphas,
        float[] offsets)
    {
        if (cellCountX <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellCountX));
        if (cellCountZ <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellCountZ));
        ArgumentNullException.ThrowIfNull(alphas);
        ArgumentNullException.ThrowIfNull(offsets);

        int expectedLength = checked(cellCountX * cellCountZ);
        if (alphas.Length != expectedLength)
            throw new ArgumentException("Alpha sample count must match the X/Z cell dimensions.", nameof(alphas));
        if (offsets.Length != expectedLength)
            throw new ArgumentException("Offset sample count must match the X/Z cell dimensions.", nameof(offsets));

        MinCellX = minCellX;
        MinCellZ = minCellZ;
        CellCountX = cellCountX;
        CellCountZ = cellCountZ;
        _alphas = (float[])alphas.Clone();
        _offsets = (float[])offsets.Clone();
    }

    public DensitySampler CreateAlphaSampler() =>
        new QuartGridSampler(this, _alphas);

    public DensitySampler CreateOffsetSampler() =>
        new QuartGridSampler(this, _offsets);

    public float GetAlpha(int blockX, int blockZ) =>
        _alphas[GetIndex(blockX, blockZ)];

    public float GetOffset(int blockX, int blockZ) =>
        _offsets[GetIndex(blockX, blockZ)];

    public static BlenderOutputBuffer CreateNeutral(DensityVolume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        int minCellX = QuartPos.FromBlock(volume.MinBlockX);
        int minCellZ = QuartPos.FromBlock(volume.MinBlockZ);
        int maxCellX = QuartPos.FromBlock(volume.MaxBlockX());
        int maxCellZ = QuartPos.FromBlock(volume.MaxBlockZ());
        int sizeX = checked(maxCellX - minCellX + 1);
        int sizeZ = checked(maxCellZ - minCellZ + 1);
        var alphas = new float[checked(sizeX * sizeZ)];
        Array.Fill(alphas, 1.0f);
        return new BlenderOutputBuffer(
            minCellX,
            minCellZ,
            sizeX,
            sizeZ,
            alphas,
            new float[alphas.Length]);
    }

    private int GetIndex(int blockX, int blockZ)
    {
        int cellX = QuartPos.FromBlock(blockX) - MinCellX;
        int cellZ = QuartPos.FromBlock(blockZ) - MinCellZ;
        if ((uint)cellX >= (uint)CellCountX || (uint)cellZ >= (uint)CellCountZ)
            throw new ArgumentOutOfRangeException(
                nameof(blockX),
                $"Block position ({blockX}, {blockZ}) lies outside the blender output volume.");

        return checked(cellX + cellZ * CellCountX);
    }

    private sealed class QuartGridSampler(
        BlenderOutputBuffer owner,
        float[] samples) : DensitySampler
    {
        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(outputBuffer);
            ArgumentNullException.ThrowIfNull(volume);

            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            {
                int blockZ = volume.BlockZ(z);
                for (int x = 0; x < volume.SizeX; x++)
                {
                    int blockX = volume.BlockX(x);
                    float value = samples[owner.GetIndex(blockX, blockZ)];
                    for (int y = 0; y < volume.SizeY; y++)
                        outputBuffer.Set(index++, value);
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            samples[owner.GetIndex(blockX, blockZ)];
    }
}

internal sealed class EmptyChunkBlender : IChunkBlender
{
    public bool IsEmpty => true;

    public float BlendDensity(int blockX, int blockY, int blockZ, float density) =>
        density;

    public BlenderOutputBuffer BlendOffsetAndFactor(DensityVolume volume) =>
        BlenderOutputBuffer.CreateNeutral(volume);
}
