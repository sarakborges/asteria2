using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

internal static class NormalNoiseChecks
{
    public static void Run()
    {
        CheckBuilderDefaultsAndRange();
        CheckNormalizationModes();
        CheckAmplitudeModifiersAndParity();
        CheckBuilderErrors();
        CheckCreatedNoise();
    }

    private static void CheckBuilderDefaultsAndRange()
    {
        NormalNoise defaults = NormalNoise.CreateBuilder().Build();
        Require(defaults.GetRange().Equals(Interval.Of(-2.0f, 2.0f)),
            "NormalNoise defaults must use one enabled octave with base amplitude one and range [-2,2].");

        NormalNoise threeOctaves = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(3)
            .Build();
        Require(threeOctaves.GetRange().Equals(Interval.Of(-2.0f, 2.0f)),
            "Enabled octave normalization must preserve unit total amplitude across three octaves.");

        NormalNoise unnormalized = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(3)
            .SetNormalize(false)
            .Build();
        Require(unnormalized.GetRange().Equals(Interval.Of(-3.5f, 3.5f)),
            "Disabled normalization must retain the geometric 1 + 1/2 + 1/4 amplitude sum.");

        NormalNoise zeroBase = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(0.0)
            .SetOctaveCount(4)
            .Build();
        Require(zeroBase.GetRange().Equals(Interval.OfExact(0.0f)),
            "A zero base amplitude must yield zero target amplitude and a zero interval.");
    }

    private static void CheckNormalizationModes()
    {
        NormalNoise.Normalization[] modes =
        {
            NormalNoise.Normalization.DISABLED,
            NormalNoise.Normalization.ENABLED,
            NormalNoise.Normalization.LEGACY
        };
        Require(Enum.GetValues<NormalNoise.Normalization>().SequenceEqual(modes),
            "NormalNoise normalization modes must preserve DISABLED, ENABLED, LEGACY order.");

        NormalNoise legacy = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(2.0)
            .SetOctaveCount(3)
            .SetAmplitudeModifier(0, 1.0)
            .SetAmplitudeModifier(1, 0.0)
            .SetAmplitudeModifier(2, 1.0)
            .SetLegacyNormalization()
            .Build();
        Require(legacy.GetRange().Equals(Interval.OfSymmetric(5.0f)),
            "Legacy normalization must apply the parity expected-deviation adjustment to the summed octave amplitudes.");

        NormalNoise enabled = NormalNoise.CreateBuilder()
            .SetBaseAmplitude(2.0)
            .SetOctaveCount(3)
            .SetAmplitudeModifier(0, 1.0)
            .SetAmplitudeModifier(1, 0.5)
            .SetAmplitudeModifier(2, 0.0)
            .Build();
        Require(enabled.GetRange().Equals(Interval.OfSymmetric((float)(2.0 * (10.0 / 7.0)))),
            "Enabled normalization must normalize base octaves first, then apply each amplitude modifier.");
    }

    private static void CheckAmplitudeModifiersAndParity()
    {
        NormalNoise parity = NormalNoise.CreateParity(-3, 1.0, 0.5, 0.0, 1.0);
        NormalNoise repeated = NormalNoise.CreateParity(-3, new[] { 1.0, 0.5, 0.0, 1.0 });
        Require(parity.Equals(repeated) && parity.GetHashCode() == repeated.GetHashCode(),
            "NormalNoise.createParity overloads must produce equal parameter configurations.");

        NormalNoise noLayers = NormalNoise.CreateBuilder()
            .SetOctaveCount(3)
            .SetAmplitudeModifier(0, 0.0)
            .SetAmplitudeModifier(1, 0.0)
            .SetAmplitudeModifier(2, 0.0)
            .Build();
        Require(noLayers.GetRange().Equals(Interval.OfExact(0.0f)),
            "Zeroed octave modifiers must remove all octave contributions and yield zero range.");

        RequireThrows<ArgumentException>(
            () => NormalNoise.CreateParity(0, Array.Empty<double>()),
            "NormalNoise.createParity must reject an empty amplitude list.");
    }

    private static void CheckBuilderErrors()
    {
        var builder = NormalNoise.CreateBuilder()
            .SetOctaveCount(3)
            .SetAmplitudeModifier(1, 0.25);
        RequireThrowsWithMessage<ArgumentException>(
            () => builder.SetOctaveCount(4),
            "Cannot set octave count after setting amplitude modifier",
            "NormalNoise.Builder.setOctaveCount must reject a call after amplitude modifiers are initialized.");

        RequireThrowsWithMessage<ArgumentException>(
            () => NormalNoise.CreateBuilder().SetOctaveCount(2).SetAmplitudeModifier(-1, 1.0),
            "-1 outside of octave range [0; 2)",
            "NormalNoise.Builder.setAmplitudeModifier must report negative indices.");
        RequireThrowsWithMessage<ArgumentException>(
            () => NormalNoise.CreateBuilder().SetOctaveCount(2).SetAmplitudeModifier(2, 1.0),
            "2 outside of octave range [0; 2)",
            "NormalNoise.Builder.setAmplitudeModifier must report the exclusive upper bound.");
    }

    private static void CheckCreatedNoise()
    {
        var firstRandom = new LegacyRandomSource(1234L);
        var secondRandom = new LegacyRandomSource(1234L);
        var firstNoise = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(4)
            .Build();
        var secondNoise = NormalNoise.CreateBuilder()
            .SetBaseOctave(-2)
            .SetOctaveCount(4)
            .Build();

        NoiseStack first = firstNoise.Create(firstRandom);
        NoiseStack second = secondNoise.Create(secondRandom);
        foreach ((double x, double y, double z) in new[]
        {
            (0.0, 0.0, 0.0),
            (1.25, -64.0, 2.5),
            (-33.75, 82.5, 91.125),
            (4096.5, -12.25, -8192.75)
        })
        {
            Require(first.Get(x, y, z) == second.Get(x, y, z),
                "NormalNoise.create must produce stable coordinate samples for identical root random streams.");
        }

        NoiseStack differentSeed = firstNoise.Create(new LegacyRandomSource(1235L));
        Require(first.Get(17.25, 64.0, -21.5) != differentSeed.Get(17.25, 64.0, -21.5),
            "NormalNoise.create must derive distinct octave streams from different root seeds.");

        NoiseStack oneActiveOctave = NormalNoise.CreateBuilder()
            .SetBaseOctave(-1)
            .SetOctaveCount(3)
            .SetAmplitudeModifier(0, 0.0)
            .SetAmplitudeModifier(1, 1.0)
            .SetAmplitudeModifier(2, 0.0)
            .Build()
            .Create(new LegacyRandomSource(555L));
        Require(float.IsFinite(oneActiveOctave.Get(17.0, 80.0, -3.0)),
            "NormalNoise.create must omit zero-modifier octaves and sample the remaining positional Perlin octave pair.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void RequireThrowsWithMessage<TException>(
        Action action,
        string expectedMessage,
        string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException exception) when (exception.Message == expectedMessage) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
