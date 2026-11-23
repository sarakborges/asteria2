namespace Asteria.World.LevelGen;

/// <summary>Minecraft Java 26.3 RandomSource contract.</summary>
public interface IRandomSource
{
    IRandomSource Fork();
    PositionalRandomFactory ForkPositional();
    void SetSeed(long seed);
    int NextInt();
    int NextInt(int bound);
    long NextLong();
    bool NextBoolean();
    float NextFloat();
    double NextDouble();
    double NextGaussian();

    int NextIntBetweenInclusive(int minimum, int maximumInclusive) =>
        unchecked(NextInt(unchecked(maximumInclusive - minimum + 1)) + minimum);

    double Triangle(double mean, double spread) =>
        mean + spread * (NextDouble() - NextDouble());

    float Triangle(float mean, float spread) =>
        mean + spread * (NextFloat() - NextFloat());

    void ConsumeCount(int rounds)
    {
        for (int i = 0; i < rounds; i++)
            _ = NextInt();
    }

    int NextInt(int origin, int bound)
    {
        if (origin >= bound)
            throw new ArgumentException("bound - origin is non positive");
        return unchecked(origin + NextInt(unchecked(bound - origin)));
    }
}
