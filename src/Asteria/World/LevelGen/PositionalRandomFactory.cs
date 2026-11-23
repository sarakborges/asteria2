using System.Text;
using Asteria.Content.Identifiers;
using Asteria.Core;

namespace Asteria.World.LevelGen;

/// <summary>Minecraft Java 26.3 positional random factory contract.</summary>
public interface PositionalRandomFactory
{
    IRandomSource At(int x, int y, int z);
    IRandomSource FromHashOf(string name);
    IRandomSource FromSeed(long seed);
    void ParityConfigString(StringBuilder builder);

    IRandomSource At(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return At(position.GetX(), position.GetY(), position.GetZ());
    }

    IRandomSource FromHashOf(ResourceLocation name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FromHashOf(name.ToString());
    }
}
