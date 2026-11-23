namespace Asteria.World.LevelGen;

/// <summary>Marsaglia polar Gaussian sampler with the cached second result from Minecraft 26.3.</summary>
public sealed class MarsagliaPolarGaussian
{
    public IRandomSource RandomSource { get; }
    private double _nextNextGaussian;
    private bool _haveNextNextGaussian;

    public MarsagliaPolarGaussian(IRandomSource randomSource) =>
        RandomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));

    public void Reset() => _haveNextNextGaussian = false;

    public double NextGaussian()
    {
        if (_haveNextNextGaussian)
        {
            _haveNextNextGaussian = false;
            return _nextNextGaussian;
        }

        double x, y, radiusSquared;
        do
        {
            x = 2.0 * RandomSource.NextDouble() - 1.0;
            y = 2.0 * RandomSource.NextDouble() - 1.0;
            radiusSquared = x * x + y * y;
        }
        while (radiusSquared >= 1.0 || radiusSquared == 0.0);

        double multiplier = Math.Sqrt(-2.0 * Math.Log(radiusSquared) / radiusSquared);
        _nextNextGaussian = y * multiplier;
        _haveNextNextGaussian = true;
        return x * multiplier;
    }
}
