using Asteria.Core;
using Asteria.Util;

namespace Asteria.World.LevelGen.DensityFunctions.Operations;

/// <summary>
/// Cell interpolation density function from Minecraft Java 26.3.
/// </summary>
public sealed record InterpolatedFunction : DensityFunction
{
    public DensityFunction Input { get; }
    public int CellSizeXz { get; }
    public int CellSizeY { get; }

    public InterpolatedFunction(DensityFunction input, int cellSizeXz, int cellSizeY)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        if (cellSizeXz <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSizeXz), cellSizeXz, "Cell size must be positive.");
        if (cellSizeY <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSizeY), cellSizeY, "Cell size must be positive.");
        CellSizeXz = cellSizeXz;
        CellSizeY = cellSizeY;
    }

    public DensitySampler CompileSampler(DensityFunction.CompileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Sampler(Input.CompileSampler(context), CellSizeXz, CellSizeY,
            1.0f / CellSizeXz, 1.0f / CellSizeY);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        DensityFunction input = rule(Input);
        return ReferenceEquals(input, Input) ? this : new InterpolatedFunction(input, CellSizeXz, CellSizeY);
    }

    public Interval Range() => Input.Range();
    public int DomainAxes() => Input.DomainAxes();

    private sealed record Sampler(
        DensitySampler Input,
        int CellSizeXz,
        int CellSizeY,
        float CellSizeXzInverse,
        float CellSizeYInverse) : DensitySampler
    {
        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(outputBuffer);
            ArgumentNullException.ThrowIfNull(volume);

            bool canSampleDirectly =
                (volume.StepBlockX == CellSizeXz || volume.SizeX == 1) &&
                (volume.StepBlockY == CellSizeY || volume.SizeY == 1) &&
                (volume.StepBlockZ == CellSizeXz || volume.SizeZ == 1) &&
                Mth.FloorMod(volume.MinBlockX, CellSizeXz) == 0 &&
                Mth.FloorMod(volume.MinBlockY, CellSizeY) == 0 &&
                Mth.FloorMod(volume.MinBlockZ, CellSizeXz) == 0;

            if (canSampleDirectly)
            {
                Input.SampleVolume(context, outputBuffer, volume);
                return;
            }

            if (volume.StepBlockX != 1 || volume.StepBlockY != 1 || volume.StepBlockZ != 1)
            {
                SampleWithNonBlockStep(context, outputBuffer, volume);
                return;
            }

            SampleWithBlockStep(context, outputBuffer, volume);
        }

        private void SampleWithNonBlockStep(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            var blockVolume = new DensityVolume(
                unchecked(volume.SizeX * volume.StepBlockX),
                unchecked(volume.SizeY * volume.StepBlockY),
                unchecked(volume.SizeZ * volume.StepBlockZ),
                volume.MinBlockX, volume.MinBlockY, volume.MinBlockZ);

            using ScopedDensityBuffer blockBuffer = context.AcquireBuffer(blockVolume);
            SampleWithBlockStep(context, blockBuffer, blockVolume);

            for (int z = 0; z < volume.SizeZ; z++)
            for (int x = 0; x < volume.SizeX; x++)
            for (int y = 0; y < volume.SizeY; y++)
            {
                float value = blockBuffer.Get(blockVolume.IndexUnchecked(
                    x * volume.StepBlockX, y * volume.StepBlockY, z * volume.StepBlockZ));
                outputBuffer.Set(volume.IndexUnchecked(x, y, z), value);
            }
        }

        private void SampleWithBlockStep(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            int minCellX = Mth.FloorDiv(volume.MinBlockX, CellSizeXz);
            int minCellY = Mth.FloorDiv(volume.MinBlockY, CellSizeY);
            int minCellZ = Mth.FloorDiv(volume.MinBlockZ, CellSizeXz);
            int maxCellX = Mth.FloorDiv(volume.MaxBlockX(), CellSizeXz);
            int maxCellY = Mth.FloorDiv(volume.MaxBlockY(), CellSizeY);
            int maxCellZ = Mth.FloorDiv(volume.MaxBlockZ(), CellSizeXz);
            int cellCountX = maxCellX - minCellX + 1;
            int cellCountY = maxCellY - minCellY + 1;
            int cellCountZ = maxCellZ - minCellZ + 1;

            var cellVolume = new DensityVolume(
                Mth.FloorMod(volume.MaxBlockX(), CellSizeXz) == 0 ? cellCountX : cellCountX + 1,
                Mth.FloorMod(volume.MaxBlockY(), CellSizeY) == 0 ? cellCountY : cellCountY + 1,
                Mth.FloorMod(volume.MaxBlockZ(), CellSizeXz) == 0 ? cellCountZ : cellCountZ + 1,
                unchecked(minCellX * CellSizeXz),
                unchecked(minCellY * CellSizeY),
                unchecked(minCellZ * CellSizeXz),
                CellSizeXz, CellSizeY, CellSizeXz);

            using ScopedDensityBuffer cellBuffer = context.AcquireBuffer(cellVolume);
            Input.SampleVolume(context, cellBuffer, cellVolume);

            for (int cellZ = 0; cellZ < cellCountZ; cellZ++)
            {
                int nextCellZ = Math.Min(cellZ + 1, cellVolume.SizeZ - 1);
                for (int cellX = 0; cellX < cellCountX; cellX++)
                {
                    int nextCellX = Math.Min(cellX + 1, cellVolume.SizeX - 1);
                    float v000 = cellBuffer.Get(cellVolume.IndexUnchecked(cellX, 0, cellZ));
                    float v100 = cellBuffer.Get(cellVolume.IndexUnchecked(nextCellX, 0, cellZ));
                    float v001 = cellBuffer.Get(cellVolume.IndexUnchecked(cellX, 0, nextCellZ));
                    float v101 = cellBuffer.Get(cellVolume.IndexUnchecked(nextCellX, 0, nextCellZ));

                    for (int cellY = 0; cellY < cellCountY; cellY++)
                    {
                        int nextCellY = Math.Min(cellY + 1, cellVolume.SizeY - 1);
                        float v010 = cellBuffer.Get(cellVolume.IndexUnchecked(cellX, nextCellY, cellZ));
                        float v110 = cellBuffer.Get(cellVolume.IndexUnchecked(nextCellX, nextCellY, cellZ));
                        float v011 = cellBuffer.Get(cellVolume.IndexUnchecked(cellX, nextCellY, nextCellZ));
                        float v111 = cellBuffer.Get(cellVolume.IndexUnchecked(nextCellX, nextCellY, nextCellZ));

                        FillCell(outputBuffer, volume, cellVolume, cellX, cellY, cellZ,
                            v000, v100, v010, v110, v001, v101, v011, v111);

                        v000 = v010;
                        v100 = v110;
                        v001 = v011;
                        v101 = v111;
                    }
                }
            }
        }

        private void FillCell(
            DensityBuffer outputBuffer, DensityVolume outputVolume, DensityVolume cellVolume,
            int cellX, int cellY, int cellZ,
            float v000, float v100, float v010, float v110,
            float v001, float v101, float v011, float v111)
        {
            int cellOutputX = cellVolume.BlockX(cellX) - outputVolume.MinBlockX;
            int cellOutputY = cellVolume.BlockY(cellY) - outputVolume.MinBlockY;
            int cellOutputZ = cellVolume.BlockZ(cellZ) - outputVolume.MinBlockZ;
            int x0 = Math.Max(0, -cellOutputX);
            int y0 = Math.Max(0, -cellOutputY);
            int z0 = Math.Max(0, -cellOutputZ);
            int x1 = Math.Min(CellSizeXz, outputVolume.SizeX - cellOutputX) - 1;
            int y1 = Math.Min(CellSizeY, outputVolume.SizeY - cellOutputY) - 1;
            int z1 = Math.Min(CellSizeXz, outputVolume.SizeZ - cellOutputZ) - 1;

            for (int z = z0; z <= z1; z++)
            {
                int outputZ = cellOutputZ + z;
                float alphaZ = z * CellSizeXzInverse;
                float v00 = Mth.Lerp(alphaZ, v000, v001);
                float v01 = Mth.Lerp(alphaZ, v010, v011);
                float v10 = Mth.Lerp(alphaZ, v100, v101);
                float v11 = Mth.Lerp(alphaZ, v110, v111);

                for (int x = x0; x <= x1; x++)
                {
                    int outputX = cellOutputX + x;
                    float alphaX = x * CellSizeXzInverse;
                    float v0 = Mth.Lerp(alphaX, v00, v10);
                    float v1 = Mth.Lerp(alphaX, v01, v11);
                    float valueStep = (v1 - v0) * CellSizeYInverse;
                    float value = v0 + valueStep * y0;
                    int outputIndex = outputVolume.IndexUnchecked(outputX, cellOutputY + y0, outputZ);

                    for (int y = y0; y <= y1; y++)
                    {
                        outputBuffer.Set(outputIndex++, value);
                        value += valueStep;
                    }
                }
            }
        }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            int xInCell = Mth.FloorMod(blockX, CellSizeXz);
            int yInCell = Mth.FloorMod(blockY, CellSizeY);
            int zInCell = Mth.FloorMod(blockZ, CellSizeXz);

            if (xInCell == 0 && yInCell == 0 && zInCell == 0)
                return Input.SampleValue(context, blockX, blockY, blockZ);

            var volume = new DensityVolume(2, 2, 2,
                blockX - xInCell, blockY - yInCell, blockZ - zInCell,
                CellSizeXz, CellSizeY, CellSizeXz);
            using ScopedDensityBuffer inputBuffer = context.AcquireBuffer(volume);
            Input.SampleVolume(context, inputBuffer, volume);

            return Mth.Lerp3(
                (float)xInCell / CellSizeXz,
                (float)yInCell / CellSizeY,
                (float)zInCell / CellSizeXz,
                inputBuffer.Get(volume.IndexUnchecked(0, 0, 0)),
                inputBuffer.Get(volume.IndexUnchecked(1, 0, 0)),
                inputBuffer.Get(volume.IndexUnchecked(0, 1, 0)),
                inputBuffer.Get(volume.IndexUnchecked(1, 1, 0)),
                inputBuffer.Get(volume.IndexUnchecked(0, 0, 1)),
                inputBuffer.Get(volume.IndexUnchecked(1, 0, 1)),
                inputBuffer.Get(volume.IndexUnchecked(0, 1, 1)),
                inputBuffer.Get(volume.IndexUnchecked(1, 1, 1)));
        }
    }
}
