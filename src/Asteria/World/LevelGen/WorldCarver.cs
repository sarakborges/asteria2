using Asteria.Content.Chunks;
using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>
/// Shape-generation contract shared by cave and canyon carvers.
/// The carver marks geometry in a CarverOutput; it does not choose the block
/// state that fills the carved position. Port of Minecraft Java 26.3 WorldCarver.
/// </summary>
public interface WorldCarver
{
    /// <summary>Maximum source-chunk reach used by the chunk carver pipeline.</summary>
    int GetRange() => 4;

    bool Carve(
        WorldGenerationContext context,
        IRandomSource random,
        ChunkPos targetChunk,
        ChunkPos sourceChunk,
        CarverOutput output);

    bool IsStartChunk(IRandomSource random);

    /// <summary>Returns this carver's data-driven parameter codec.</summary>
    MapCodec<WorldCarver> GetCodec();

    /// <summary>
    /// Marks an ellipsoid clipped to the target chunk and the output's vertical range.
    /// The skip predicate receives normalized X/Y/Z coordinates and the world Y.
    /// </summary>
    static void CarveEllipsoid(
        ChunkPos chunkPosition,
        double x,
        double y,
        double z,
        double horizontalRadius,
        double verticalRadius,
        CarverOutput output,
        CarveSkipChecker skipChecker)
    {
        ArgumentNullException.ThrowIfNull(chunkPosition);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(skipChecker);

        double centerX = chunkPosition.GetMiddleBlockX();
        double centerZ = chunkPosition.GetMiddleBlockZ();
        double maxDelta = 16.0 + horizontalRadius * 2.0;

        if (!(Math.Abs(x - centerX) > maxDelta) &&
            !(Math.Abs(z - centerZ) > maxDelta))
        {
            int chunkMinX = chunkPosition.GetMinBlockX();
            int chunkMinZ = chunkPosition.GetMinBlockZ();

            int minXIndex = Math.Max(
                unchecked(Mth.Floor(x - horizontalRadius) - chunkMinX - 1),
                0);
            int maxXIndex = Math.Min(
                unchecked(Mth.Floor(x + horizontalRadius) - chunkMinX),
                15);
            int minY = Math.Max(
                unchecked(Mth.Floor(y - verticalRadius) - 1),
                output.MinY());
            int maxY = Math.Min(
                unchecked(Mth.Floor(y + verticalRadius) + 1),
                output.MaxY());
            int minZIndex = Math.Max(
                unchecked(Mth.Floor(z - horizontalRadius) - chunkMinZ - 1),
                0);
            int maxZIndex = Math.Min(
                unchecked(Mth.Floor(z + horizontalRadius) - chunkMinZ),
                15);

            for (int xIndex = minXIndex; xIndex <= maxXIndex; xIndex++)
            {
                int worldX = chunkPosition.GetBlockX(xIndex);
                double xd = (worldX + 0.5 - x) / horizontalRadius;

                for (int zIndex = minZIndex; zIndex <= maxZIndex; zIndex++)
                {
                    int worldZ = chunkPosition.GetBlockZ(zIndex);
                    double zd = (worldZ + 0.5 - z) / horizontalRadius;
                    if (!(xd * xd + zd * zd >= 1.0))
                    {
                        for (int worldY = maxY; worldY > minY; worldY--)
                        {
                            double yd = (worldY - 0.5 - y) / verticalRadius;
                            if (!skipChecker.ShouldSkip(xd, yd, zd, worldY))
                                output.Carve(xIndex, worldY, zIndex);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tests whether a carving walk can still reach the target chunk from its current
    /// horizontal position within the remaining steps.
    /// </summary>
    static bool CanReach(
        ChunkPos chunkPosition,
        double x,
        double z,
        int currentStep,
        int totalSteps,
        float thickness)
    {
        ArgumentNullException.ThrowIfNull(chunkPosition);

        double xMid = chunkPosition.GetMiddleBlockX();
        double zMid = chunkPosition.GetMiddleBlockZ();
        double xd = x - xMid;
        double zd = z - zMid;
        double remaining = (double)totalSteps - currentStep;
        double radius = thickness + 2.0f + 16.0f;

        return xd * xd + zd * zd - remaining * remaining <= radius * radius;
    }

    public delegate bool CarveSkipChecker(double xd, double yd, double zd, int y);
}
