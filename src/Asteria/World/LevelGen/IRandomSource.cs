namespace Asteria.World.LevelGen;

public interface IRandomSource
{
    int NextInt(int bound);

    float NextFloat();
}
