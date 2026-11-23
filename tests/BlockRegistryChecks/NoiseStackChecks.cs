using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

internal static class NoiseStackChecks
{
    public static void Run()
    {
        var first = new PerlinNoise(new WorldgenRandom(new LegacyRandomSource(1234L)));
        var second = new PerlinNoise(new WorldgenRandom(new LegacyRandomSource(5678L)));
        NoiseStack stack = NoiseStack.CreateBuilder()
            .Add(first, 0.5, 1.25f)
            .Add(second, 2.0, -0.5f)
            .Build();

        Require(stack.GetRange().Equals(Interval.Of(-3.5f, 3.5f)),
            "NoiseStack.range must sum each layer range after multiplying by its signed amplitude.");
        Require(stack.Get(1.25, -2.5, 3.75) ==
                1.25f * first.Get(1.25 * 0.5, -2.5 * 0.5, 3.75 * 0.5) -
                0.5f * second.Get(1.25 * 2.0, -2.5 * 2.0, 3.75 * 2.0),
            "NoiseStack.get(3D) must apply each layer's frequency before sampling and preserve layer order.");
        Require(stack.Get(1.25, -2.5) ==
                1.25f * first.Get(1.25 * 0.5, -2.5 * 0.5) -
                0.5f * second.Get(1.25 * 2.0, -2.5 * 2.0),
            "NoiseStack.get(2D) must forward to the corresponding two-coordinate noise samplers.");
        Require(ReferenceEquals(stack.GetLayer(0), first) && ReferenceEquals(stack.GetLayer(1), second),
            "NoiseStack.getLayer must return the exact noise layer in insertion order.");
        Require(stack.GetType().Name == "Perlin",
            "NoiseStack.Builder must select the Perlin volume stack when every layer is exactly PerlinNoise.");

        var child = NoiseStack.CreateBuilder()
            .Add(first, 0.25, 2.0f)
            .Build();
        NoiseStack flattened = NoiseStack.CreateBuilder()
            .AddStack(child, 3.0, 0.5f)
            .Build();
        Require(flattened.Get(2.0, 4.0, 6.0) == first.Get(1.5, 3.0, 4.5) &&
            flattened.GetRange().Equals(Interval.Of(-2.0f, 2.0f)),
            "NoiseStack.addStack must flatten child layers and multiply their frequencies and amplitudes.");

        var volume = new DensityVolume(2, 3, 2, -2, 1, 5);
        DensityBuffer stackBuffer = DensityBuffer.CreateUnpooled(volume.Size());
        stack.AddToVolume(stackBuffer, volume, 0.125, 0.25, 0.75f);

        DensityBuffer expectedFirst = DensityBuffer.CreateUnpooled(volume.Size());
        first.AddToVolume(expectedFirst, volume, 0.125 * 0.5, 0.25 * 0.5, 0.75f * 1.25f);
        DensityBuffer expectedSecond = DensityBuffer.CreateUnpooled(volume.Size());
        second.AddToVolume(expectedSecond, volume, 0.125 * 2.0, 0.25 * 2.0, 0.75f * -0.5f);
        for (int index = 0; index < volume.Size(); index++)
        {
            Require(Math.Abs(stackBuffer.Get(index) - (expectedFirst.Get(index) + expectedSecond.Get(index))) < 0.000001f,
                $"NoiseStack.addToVolume must delegate each layer with scaled frequency and amplitude at index {index}.");
        }

        DensityBuffer untouched = DensityBuffer.CreateUnpooled(volume.Size());
        untouched.Fill(7.0f);
        NoiseStack.CreateBuilder().Build().AddToVolume(untouched, volume, 1.0, 1.0, 1.0f);
        Require(Enumerable.Range(0, untouched.Size()).All(index => untouched.Get(index) == 7.0f),
            "An empty NoiseStack must leave an existing density buffer unchanged.");

        var simplex = new SimplexNoise(new WorldgenRandom(new LegacyRandomSource(2345L)), discardNoiseOffset: true);
        NoiseStack mixed = NoiseStack.CreateBuilder()
            .Add(first, 0.75, 0.5f)
            .Add(simplex, 1.25, 0.25f)
            .Build();
        Require(mixed.GetType() == typeof(NoiseStack) &&
            mixed.GetRange().Equals(Interval.Of(-1.5f, 1.5f)),
            "Mixed noise types must use the generic NoiseStack with the summed interval range.");

        RequireThrows<IndexOutOfRangeException>(() => stack.GetLayer(2),
            "NoiseStack.getLayer must preserve the underlying layer-array bounds behavior.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
