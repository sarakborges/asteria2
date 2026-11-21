namespace Asteria.World.LevelGen.Synth;

/// <summary>Ordered, amplitude-weighted stack of two-dimensional noise layers.</summary>
public sealed class NoiseStack
{
    private readonly Layer[] _layers;

    private NoiseStack(Layer[] layers) => _layers = layers;

    public float Get(double x, double z)
    {
        float value = 0.0F;
        foreach (Layer layer in _layers)
        {
            double frequency = layer.Frequency;
            value += layer.Amplitude * layer.Noise.Get(x * frequency, z * frequency);
        }

        return value;
    }

    private readonly record struct Layer(SimplexNoise Noise, double Frequency, float Amplitude);

    public sealed class Builder
    {
        private readonly List<Layer> _layers = [];

        public Builder Add(SimplexNoise noise, double frequency, float amplitude)
        {
            ArgumentNullException.ThrowIfNull(noise);
            _layers.Add(new Layer(noise, frequency, amplitude));
            return this;
        }

        public NoiseStack Build() => new(_layers.ToArray());
    }
}
