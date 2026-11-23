using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Ordered amplitude-weighted noise layers, matching Minecraft Java 26.3 NoiseStack.
/// </summary>
public class NoiseStack : Noise
{
    protected readonly Layer[] Layers;
    private readonly Interval _range;

    protected NoiseStack(Layer[] layers)
    {
        Layers = layers;
        Interval range = Interval.OfExact(0.0f);

        foreach (Layer layer in Layers)
        {
            Interval layerRange = Interval.Mul(
                layer.Noise.GetRange(),
                Interval.OfExact(layer.Amplitude));
            range = Interval.Add(range, layerRange);
        }

        _range = range;
    }

    public static Builder CreateBuilder() => new();

    public Interval GetRange() => _range;

    public float Get(double x, double y, double z)
    {
        float value = 0.0f;
        foreach (Layer layer in Layers)
        {
            double frequency = layer.Frequency;
            value += layer.Amplitude * layer.Noise.Get(
                x * frequency,
                y * frequency,
                z * frequency);
        }

        return value;
    }

    public float Get(double x, double y)
    {
        float value = 0.0f;
        foreach (Layer layer in Layers)
        {
            double frequency = layer.Frequency;
            value += layer.Amplitude * layer.Noise.Get(x * frequency, y * frequency);
        }

        return value;
    }

    public virtual void AddToVolume(
        DensityBuffer buffer,
        DensityVolume volume,
        double xzScale,
        double yScale,
        float amplitude)
    {
        foreach (Layer layer in Layers)
        {
            double frequency = layer.Frequency;
            layer.Noise.AddToVolume(
                buffer,
                volume,
                xzScale * frequency,
                yScale * frequency,
                amplitude * layer.Amplitude);
        }
    }

    public Noise GetLayer(int index) => Layers[index].Noise;

    public sealed class Builder
    {
        private readonly List<Layer> _layers = [];

        public Builder Add(Noise noise, double frequency, float amplitude)
        {
            _layers.Add(new Layer(noise, frequency, amplitude));
            return this;
        }

        public Builder AddStack(NoiseStack stack, double frequency, float amplitude)
        {
            foreach (Layer layer in stack.Layers)
            {
                _layers.Add(new Layer(
                    layer.Noise,
                    layer.Frequency * frequency,
                    layer.Amplitude * amplitude));
            }

            return this;
        }

        public NoiseStack Build()
        {
            Layer[] layers = _layers.ToArray();
            if (layers.All(layer => layer.Noise.GetType() == typeof(PerlinNoise)))
                return new Perlin(layers);

            return new NoiseStack(layers);
        }
    }

    protected sealed record Layer(Noise Noise, double Frequency, float Amplitude);

    private sealed class Perlin : NoiseStack
    {
        public Perlin(Layer[] layers)
            : base(layers)
        {
        }

        public override void AddToVolume(
            DensityBuffer buffer,
            DensityVolume volume,
            double xzScale,
            double yScale,
            float amplitude)
        {
            foreach (Layer layer in Layers)
            {
                double frequency = layer.Frequency;
                layer.Noise.AddToVolume(
                    buffer,
                    volume,
                    xzScale * frequency,
                    yScale * frequency,
                    amplitude * layer.Amplitude);
            }
        }
    }
}
