using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Structure;
using Asteria.World.LevelGen.Structure.Pools;
using Asteria.Util.Context;

namespace Asteria.World.LevelGen;

/// <summary>
/// Structure terrain-adaptation density sampler from Minecraft Java 26.3 Beardifier.
/// The sampling kernel is complete; gathering structure starts and pool-piece metadata
/// remains in the structure manager/pool pipeline, which is not yet ported.
/// </summary>
public sealed class Beardifier : DensitySampler
{
    public static ContextKey<Beardifier> CONTEXT_KEY { get; } =
        ContextKey<Beardifier>.Vanilla("beardifier");

    public static Interval RANGE { get; } = Interval.INFINITE;

    public const int BEARD_KERNEL_RADIUS = 12;

    private const int BEARD_KERNEL_SIZE = BEARD_KERNEL_RADIUS * 2;
    private const float MAX_BURY_DISTANCE = 6.0F;

    private static readonly float[] BEARD_KERNEL = CreateBeardKernel();

    public static Beardifier EMPTY { get; } =
        new(Array.Empty<Rigid>(), Array.Empty<JigsawJunction>(), null);

    private readonly IReadOnlyList<Rigid> _pieces;
    private readonly IReadOnlyList<JigsawJunction> _junctions;
    private readonly BoundingBox? _affectedBox;

    /// <summary>
    /// A rigid structure piece with its terrain adjustment and ground-level offset.
    /// </summary>
    public sealed record Rigid(
        BoundingBox Box,
        TerrainAdjustment TerrainAdjustment,
        int GroundLevelDelta);

    public Beardifier(
        IReadOnlyList<Rigid> pieces,
        IReadOnlyList<JigsawJunction> junctions,
        BoundingBox? affectedBox)
    {
        _pieces = pieces;
        _junctions = junctions;
        _affectedBox = affectedBox;
    }

    public void SampleVolume(
        SamplerContext context,
        DensityBuffer outputBuffer,
        DensityVolume volume)
    {
        outputBuffer.Fill(0.0F);

        if (_affectedBox is not null && volume.Intersects(_affectedBox))
        {
            int minX = Mth.FloorDiv(
                Math.Max(0, unchecked(_affectedBox.MinX() - volume.MinBlockX)),
                volume.StepBlockX);
            int minY = Mth.FloorDiv(
                Math.Max(0, unchecked(_affectedBox.MinY() - volume.MinBlockY)),
                volume.StepBlockY);
            int minZ = Mth.FloorDiv(
                Math.Max(0, unchecked(_affectedBox.MinZ() - volume.MinBlockZ)),
                volume.StepBlockZ);
            int maxX = Math.Min(
                volume.SizeX - 1,
                Mth.FloorDiv(
                    unchecked(_affectedBox.MaxX() - volume.MinBlockX),
                    volume.StepBlockX));
            int maxY = Math.Min(
                volume.SizeY - 1,
                Mth.FloorDiv(
                    unchecked(_affectedBox.MaxY() - volume.MinBlockY),
                    volume.StepBlockY));
            int maxZ = Math.Min(
                volume.SizeZ - 1,
                Mth.FloorDiv(
                    unchecked(_affectedBox.MaxZ() - volume.MinBlockZ),
                    volume.StepBlockZ));

            for (int z = minZ; z <= maxZ; z++)
            {
                int blockZ = volume.BlockZ(z);
                for (int x = minX; x <= maxX; x++)
                {
                    int blockX = volume.BlockX(x);
                    for (int y = minY; y <= maxY; y++)
                    {
                        int index = volume.IndexUnchecked(x, y, z);
                        int blockY = volume.BlockY(y);
                        outputBuffer.Set(
                            index,
                            SampleValueUnchecked(blockX, blockY, blockZ));
                    }
                }
            }
        }
    }

    public float SampleValue(
        SamplerContext context,
        int blockX,
        int blockY,
        int blockZ) =>
        _affectedBox is not null && _affectedBox.IsInside(blockX, blockY, blockZ)
            ? SampleValueUnchecked(blockX, blockY, blockZ)
            : 0.0F;

    private float SampleValueUnchecked(int blockX, int blockY, int blockZ)
    {
        float noiseValue = 0.0F;

        foreach (Rigid rigid in _pieces)
        {
            BoundingBox box = rigid.Box;
            int groundLevelDelta = rigid.GroundLevelDelta;
            int dx = Math.Max(
                0,
                Math.Max(
                    unchecked(box.MinX() - blockX),
                    unchecked(blockX - box.MaxX())));
            int dz = Math.Max(
                0,
                Math.Max(
                    unchecked(box.MinZ() - blockZ),
                    unchecked(blockZ - box.MaxZ())));
            int groundY = unchecked(box.MinY() + groundLevelDelta);
            int dyToGround = unchecked(blockY - groundY);

            int dy;
            if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.NONE))
            {
                dy = 0;
            }
            else if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BURY) ||
                     ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BEARD_THIN))
            {
                dy = dyToGround;
            }
            else if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BEARD_BOX))
            {
                dy = Math.Max(
                    0,
                    Math.Max(
                        unchecked(groundY - blockY),
                        unchecked(blockY - box.MaxY())));
            }
            else if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.ENCAPSULATE))
            {
                dy = Math.Max(
                    0,
                    Math.Max(
                        unchecked(box.MinY() - blockY),
                        unchecked(blockY - box.MaxY())));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown terrain adjustment: {rigid.TerrainAdjustment}");
            }

            if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.NONE))
            {
                noiseValue += 0.0F;
            }
            else if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BURY))
            {
                noiseValue += GetBuryContribution(dx, dy / 2.0F, dz);
            }
            else if (ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BEARD_THIN) ||
                     ReferenceEquals(rigid.TerrainAdjustment, TerrainAdjustment.BEARD_BOX))
            {
                noiseValue += GetBeardContribution(dx, dy, dz, dyToGround) * 0.8F;
            }
            else
            {
                noiseValue +=
                    GetBuryContribution(dx / 2.0F, dy / 2.0F, dz / 2.0F) * 0.8F;
            }
        }

        foreach (JigsawJunction junction in _junctions)
        {
            int dx = unchecked(blockX - junction.GetSourceX());
            int dy = unchecked(blockY - junction.GetSourceGroundY());
            int dz = unchecked(blockZ - junction.GetSourceZ());
            noiseValue += GetBeardContribution(dx, dy, dz, dy) * 0.4F;
        }

        return noiseValue;
    }

    private static float GetBuryContribution(float dx, float dy, float dz)
    {
        float distanceSquared = Mth.LengthSquared(dx, dy, dz);
        return distanceSquared >= 36.0F
            ? 0.0F
            : 1.0F - Mth.Sqrt(distanceSquared) / MAX_BURY_DISTANCE;
    }

    private static float GetBeardContribution(int dx, int dy, int dz, int yToGround)
    {
        int xi = dx + BEARD_KERNEL_RADIUS;
        int yi = dy + BEARD_KERNEL_RADIUS;
        int zi = dz + BEARD_KERNEL_RADIUS;

        if (!IsInKernelRange(xi) ||
            !IsInKernelRange(yi) ||
            !IsInKernelRange(zi))
        {
            return 0.0F;
        }

        float dyWithOffset = yToGround + 0.5F;
        float distanceSquared = Mth.LengthSquared((float)dx, dyWithOffset, (float)dz);
        float value =
            -dyWithOffset *
            (float)Mth.FastInvSqrt(distanceSquared / 2.0F) /
            2.0F;

        return value * BEARD_KERNEL[zi * BEARD_KERNEL_SIZE * BEARD_KERNEL_SIZE +
                                    xi * BEARD_KERNEL_SIZE +
                                    yi];
    }

    private static bool IsInKernelRange(int index) =>
        index >= 0 && index < BEARD_KERNEL_SIZE;

    private static float[] CreateBeardKernel()
    {
        var kernel = new float[BEARD_KERNEL_SIZE * BEARD_KERNEL_SIZE * BEARD_KERNEL_SIZE];

        for (int zi = 0; zi < BEARD_KERNEL_SIZE; zi++)
        {
            for (int xi = 0; xi < BEARD_KERNEL_SIZE; xi++)
            {
                for (int yi = 0; yi < BEARD_KERNEL_SIZE; yi++)
                {
                    kernel[zi * BEARD_KERNEL_SIZE * BEARD_KERNEL_SIZE +
                           xi * BEARD_KERNEL_SIZE +
                           yi] =
                        (float)ComputeBeardContribution(
                            xi - BEARD_KERNEL_RADIUS,
                            yi - BEARD_KERNEL_RADIUS,
                            zi - BEARD_KERNEL_RADIUS);
                }
            }
        }

        return kernel;
    }

    private static double ComputeBeardContribution(int dx, int dy, int dz) =>
        ComputeBeardContribution(dx, dy + 0.5, dz);

    private static double ComputeBeardContribution(int dx, double dy, int dz)
    {
        double distanceSquared = Mth.LengthSquared(dx, dy, dz);
        return Math.Pow(Math.E, -distanceSquared / 16.0);
    }
}
