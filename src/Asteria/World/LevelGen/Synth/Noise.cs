using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Minecraft 26.3 noise contract. Implementations can evaluate points and optionally
/// override the generic volume traversal with a cache-aware path.
/// </summary>
public interface Noise
{
    Interval GetRange();

    float Get(double x, double y);

    float Get(double x, double y, double z);

    void AddToVolume(
        DensityBuffer buffer,
        DensityVolume volume,
        double xzScale,
        double yScale,
        float amplitude)
    {
        int index = 0;

        for (int indexZ = 0; indexZ < volume.SizeZ; indexZ++)
        {
            double z = volume.BlockZ(indexZ) * xzScale;
            for (int indexX = 0; indexX < volume.SizeX; indexX++)
            {
                double x = volume.BlockX(indexX) * xzScale;
                for (int indexY = 0; indexY < volume.SizeY; indexY++)
                {
                    double y = volume.BlockY(indexY) * yScale;
                    buffer.AddTo(index, amplitude * Get(x, y, z));
                    index++;
                }
            }
        }
    }
}
