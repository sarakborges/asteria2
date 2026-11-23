using System.Globalization;
using System.Text;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Multi-octave Perlin noise parameterization from Minecraft Java 26.3.
/// </summary>
public sealed class NormalNoise : IEquatable<NormalNoise>
{
    private const double InputFactor = 1.0181268882175227;
    public const double TARGET_DEVIATION = 0.3333333333333333;
    public const double MAX_AMPLITUDE = 1000000.0;
    public const int MAX_OCTAVE_COUNT = 32;
    public const double PERSISTENCE = 0.5;
    public const double LACUNARITY = 2.0;

    private static Codec<Normalization> NormalizationCodec { get; } =
        new NormalizationCodecImpl();

    public static Codec<NormalNoise> DIRECT_CODEC { get; } =
        Parameters.CODEC.XMap<NormalNoise>(
            static parameters => new NormalNoise(parameters),
            static noise => noise._parameters);

    public static Codec<Holder<NormalNoise>> CODEC { get; } =
        RegistryCodecs.Holder(Registries.NOISE, DIRECT_CODEC);

    private readonly Parameters _parameters;
    private readonly OctaveInfo[] _octaves;
    private readonly double _normalizationFactor;
    private readonly Interval _range;

    private NormalNoise(Parameters parameters)
    {
        _parameters = parameters;
        _octaves = BuildOctaves(
            parameters.BaseOctave,
            parameters.BaseAmplitude,
            parameters.OctaveCount,
            parameters.Normalize != Normalization.DISABLED,
            parameters.AmplitudeModifiers);

        double targetAmplitude = _octaves.Sum(octave => octave.AbsAmplitude());
        double normalizationFactor = ComputeNormalizationFactor(targetAmplitude, _octaves);

        if (parameters.Normalize == Normalization.LEGACY && normalizationFactor != 0.0)
        {
            double parityNormalizationFactor = ComputeParityNormalizationFactor(
                parameters.BaseAmplitude,
                parameters.OctaveCount,
                parameters.AmplitudeModifiers);
            targetAmplitude *= parityNormalizationFactor / normalizationFactor;
            normalizationFactor = parityNormalizationFactor;
        }

        _normalizationFactor = normalizationFactor;
        _range = Interval.OfSymmetric((float)(targetAmplitude * TARGET_DEVIATION * 6.0));
    }

    public static Builder CreateBuilder() => new();

    public Interval GetRange() => _range;

    public NoiseStack Create(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        PositionalRandomFactory firstRandom = random.ForkPositional();
        PositionalRandomFactory secondRandom = random.ForkPositional();
        NoiseStack.Builder stack = NoiseStack.CreateBuilder();

        foreach (OctaveInfo octave in _octaves)
        {
            string octaveSeed = octave.Seed();
            Noise firstNoise = new PerlinNoise(firstRandom.FromHashOf(octaveSeed));
            Noise secondNoise = new PerlinNoise(secondRandom.FromHashOf(octaveSeed));
            float valueFactor = (float)(_normalizationFactor * octave.Amplitude);

            stack.Add(firstNoise, octave.Frequency, valueFactor);
            stack.Add(secondNoise, octave.Frequency * InputFactor, valueFactor);
        }

        return stack.Build();
    }

    /// <summary>
    /// Reconstructs legacy amplitude-array configurations using the 26.3 parity normalization formula.
    /// </summary>
    public static NormalNoise CreateParity(int firstOctave, IReadOnlyList<double> amplitudes)
    {
        ArgumentNullException.ThrowIfNull(amplitudes);
        if (amplitudes.Count == 0)
            throw new ArgumentException("Need at least 1 amplitude", nameof(amplitudes));

        Builder builder = CreateBuilder()
            .SetBaseOctave(firstOctave)
            .SetOctaveCount(amplitudes.Count)
            .SetBaseAmplitude(ComputeParityBaseAmplitude(firstOctave, amplitudes));

        for (int index = 0; index < amplitudes.Count; index++)
        {
            double modifier = amplitudes[index];
            if (modifier != 1.0)
                builder.SetAmplitudeModifier(index, modifier);
        }

        return builder.Build();
    }

    public static NormalNoise CreateParity(int firstOctave, params double[] amplitudes) =>
        CreateParity(firstOctave, (IReadOnlyList<double>)amplitudes);

    private static OctaveInfo[] BuildOctaves(
        int baseOctave,
        double baseAmplitude,
        int octaveCount,
        bool normalize,
        IReadOnlyList<double> amplitudeModifiers)
    {
        double frequency = Math.Pow(2.0, baseOctave);
        double amplitude = baseAmplitude;

        if (normalize)
        {
            amplitude *= Math.Pow(0.5, -(octaveCount - 1)) /
                (Math.Pow(0.5, -octaveCount) - 1.0);
        }

        var octaves = new List<OctaveInfo>(octaveCount);
        for (int index = 0; index < octaveCount; index++)
        {
            double amplitudeModifier = GetAmplitudeModifier(amplitudeModifiers, index);
            if (amplitudeModifier != 0.0)
            {
                double modifiedAmplitude = amplitude * amplitudeModifier;
                octaves.Add(new OctaveInfo(
                    unchecked(baseOctave + index),
                    frequency,
                    modifiedAmplitude));
            }

            frequency *= 2.0;
            amplitude *= 0.5;
        }

        return octaves.ToArray();
    }

    private static double GetAmplitudeModifier(IReadOnlyList<double> amplitudeModifiers, int index) =>
        amplitudeModifiers.Count == 0 ? 1.0 : amplitudeModifiers[index];

    private static double ComputeNormalizationFactor(double targetAmplitude, IReadOnlyList<OctaveInfo> octaves)
    {
        double inputDeviation = EstimateDeviation(octaves);
        if (inputDeviation == 0.0)
            return 0.0;

        double inputSumDeviation = inputDeviation * Math.Sqrt(2.0);
        double targetDeviation = targetAmplitude * TARGET_DEVIATION;
        return targetDeviation / inputSumDeviation;
    }

    private static double EstimateDeviation(IEnumerable<OctaveInfo> octaves)
    {
        double variance = 0.0;
        foreach (OctaveInfo octave in octaves)
        {
            double layerDeviation = PerlinNoise.STANDARD_DEVIATION * octave.AbsAmplitude();
            variance += layerDeviation * layerDeviation;
        }

        return Math.Sqrt(variance);
    }

    private static double ComputeParityBaseAmplitude(int baseOctave, IReadOnlyList<double> amplitudes)
    {
        OctaveInfo[] octaves = BuildOctaves(baseOctave, 1.0, amplitudes.Count, true, amplitudes);
        double targetAmplitude = octaves.Sum(octave => octave.AbsAmplitude());
        double newNormalizationFactor = ComputeNormalizationFactor(targetAmplitude, octaves);
        if (newNormalizationFactor == 0.0)
            return 1.0;

        double oldNormalizationFactor =
            ComputeParityNormalizationFactor(1.0, amplitudes.Count, amplitudes);
        return oldNormalizationFactor / newNormalizationFactor;
    }

    private static double ComputeParityNormalizationFactor(
        double baseAmplitude,
        int octaveCount,
        IReadOnlyList<double> amplitudeModifiers)
    {
        int minOctave = int.MaxValue;
        int maxOctave = int.MinValue;

        for (int index = 0; index < octaveCount; index++)
        {
            double modifier = GetAmplitudeModifier(amplitudeModifiers, index);
            if (modifier != 0.0)
            {
                minOctave = Math.Min(minOctave, index);
                maxOctave = Math.Max(maxOctave, index);
            }
        }

        return baseAmplitude * 0.5 * TARGET_DEVIATION /
            ParityExpectedDeviation(unchecked(maxOctave - minOctave));
    }

    private static double ParityExpectedDeviation(int octaveSpan) =>
        0.1 * (1.0 + 1.0 / (octaveSpan + 1));

    public void ParityConfigString(StringBuilder builder, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(random);

        builder.Append("NormalNoise {");
        builder.Append("first: ");
        PerlinParityConfigString(builder, random);
        builder.Append(", second: ");
        PerlinParityConfigString(builder, random);
        builder.Append("}");
    }

    private void PerlinParityConfigString(StringBuilder builder, IRandomSource random)
    {
        builder.Append("PerlinNoise{");
        var amplitudeStrings = new string[_parameters.OctaveCount];
        for (int index = 0; index < amplitudeStrings.Length; index++)
        {
            amplitudeStrings[index] = GetAmplitudeModifier(_parameters.AmplitudeModifiers, index)
                .ToString("F2", CultureInfo.InvariantCulture);
        }

        builder.Append("first octave: ")
            .Append(_parameters.BaseOctave)
            .Append(", amplitudes: [")
            .Append(string.Join(", ", amplitudeStrings))
            .Append("], noise levels: [");

        PositionalRandomFactory positional = random.ForkPositional();
        for (int index = 0; index < _parameters.OctaveCount; index++)
        {
            builder.Append(index).Append(": ");
            int octaveIndex = unchecked(_parameters.BaseOctave + index);
            OctaveInfo? octave = _octaves.FirstOrDefault(info => info.OctaveIndex == octaveIndex);
            if (octave is not null)
            {
                var noise = new PerlinNoise(positional.FromHashOf(octave.Seed()));
                noise.ParityConfigString(builder);
            }
            else
            {
                builder.Append("null");
            }

            builder.Append(", ");
        }

        builder.Append("]}");
    }

    /// <summary>
    /// Recreates the legacy Nether biome fBm pipeline, including its fixed random-consumption
    /// rules for absent octaves. This is intentionally distinct from the modern Create path.
    /// </summary>
    [Obsolete("Legacy Nether biome noise compatibility path.")]
    public NoiseStack CreateForLegacyNetherBiome(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        IReadOnlyList<double> amplitudes = _parameters.AmplitudeModifiers.Count == 0
            ? Enumerable.Repeat(1.0, _parameters.OctaveCount).ToArray()
            : _parameters.AmplitudeModifiers;

        NoiseStack first = LegacyFbmInitializer.CreateForLegacyNetherBiome(
            random, _parameters.BaseOctave, amplitudes);
        NoiseStack second = LegacyFbmInitializer.CreateForLegacyNetherBiome(
            random, _parameters.BaseOctave, amplitudes);

        float valueFactor = (float)(_normalizationFactor * _parameters.BaseAmplitude);
        return NoiseStack.CreateBuilder()
            .AddStack(first, 1.0, valueFactor)
            .AddStack(second, InputFactor, valueFactor)
            .Build();
    }

    public bool Equals(NormalNoise? other) =>
        ReferenceEquals(this, other) ||
        (other is not null && _parameters.Equals(other._parameters));

    public override bool Equals(object? obj) =>
        obj is NormalNoise other && Equals(other);

    public override int GetHashCode() => _parameters.GetHashCode();

    public override string ToString() =>
        $"NormalNoise[parameters={_parameters}]";

    private sealed record OctaveInfo(int OctaveIndex, double Frequency, double Amplitude)
    {
        public string Seed() => "octave_" + OctaveIndex;
        public double AbsAmplitude() => Math.Abs(Amplitude);
    }

    private sealed record NormalizationSettings(
        Normalization Normalize,
        IReadOnlyList<double> AmplitudeModifiers);

    private static MapCodec<NormalizationSettings> CreateNormalizationSettingsCodec() =>
        RecordCodecBuilder.MapCodec<NormalizationSettings>(instance =>
            instance.Group(
                    NormalizationCodec.OptionalFieldOf(
                            "normalize",
                            Normalization.ENABLED)
                        .ForGetter<NormalizationSettings>(static settings => settings.Normalize),
                    Codecs.DoubleRange(0.0, MAX_AMPLITUDE)
                        .ListOf(0, MAX_OCTAVE_COUNT)
                        .OptionalFieldOf("amplitude_modifiers", Array.Empty<double>())
                        .ForGetter<NormalizationSettings>(static settings => settings.AmplitudeModifiers))
                .Apply(
                    instance,
                    static (normalize, amplitudeModifiers) =>
                        new NormalizationSettings(normalize, amplitudeModifiers)));

    private sealed class NormalizationCodecImpl : Codec<Normalization>
    {
        public override DataResult<TData> Encode<TData>(
            Normalization input,
            DynamicOps<TData> ops,
            TData prefix) =>
            input switch
            {
                Normalization.DISABLED => Codecs.BOOL.Encode(false, ops, prefix),
                Normalization.ENABLED => Codecs.BOOL.Encode(true, ops, prefix),
                Normalization.LEGACY => Codecs.STRING.Encode("legacy", ops, prefix),
                _ => DataResult<TData>.Error(
                    () => $"Unknown normalization type: {input}")
            };

        public override DataResult<Pair<Normalization, TData>> Decode<TData>(
            DynamicOps<TData> ops,
            TData input)
        {
            DataResult<Pair<bool, TData>> boolean = Codecs.BOOL.Decode(ops, input);
            if (boolean.TryGetResult(out Pair<bool, TData> boolPair))
            {
                return DataResult<Pair<Normalization, TData>>.Success(
                    new Pair<Normalization, TData>(
                        boolPair.First ? Normalization.ENABLED : Normalization.DISABLED,
                        boolPair.Second));
            }

            DataResult<Pair<string, TData>> text = Codecs.STRING.Decode(ops, input);
            if (text.TryGetResult(out Pair<string, TData> textPair))
            {
                return string.Equals(textPair.First, "legacy", StringComparison.Ordinal)
                    ? DataResult<Pair<Normalization, TData>>.Success(
                        new Pair<Normalization, TData>(Normalization.LEGACY, textPair.Second))
                    : DataResult<Pair<Normalization, TData>>.Error(
                        () => $"Invalid normalization type: {textPair.First}");
            }

            return DataResult<Pair<Normalization, TData>>.Error(
                () => $"Invalid normalization type: {boolean.ErrorMessage}; {text.ErrorMessage}");
        }

        public override string ToString() =>
            "Normalization[bool|legacy]";
    }

    private sealed class Parameters : IEquatable<Parameters>
    {
        public static Codec<Parameters> CODEC { get; } =
            RecordCodecBuilder.Create<Parameters>(instance =>
                instance.Group(
                        Codecs.DoubleRange(1.0E-5f, MAX_AMPLITUDE)
                            .OptionalFieldOf("base_amplitude", 1.0)
                            .ForGetter<Parameters>(static parameters => parameters.BaseAmplitude),
                        Codecs.IntRange(-MAX_OCTAVE_COUNT, MAX_OCTAVE_COUNT)
                            .FieldOf("base_octave")
                            .ForGetter<Parameters>(static parameters => parameters.BaseOctave),
                        Codecs.IntRange(1, MAX_OCTAVE_COUNT)
                            .OptionalFieldOf("octave_count", 1)
                            .ForGetter<Parameters>(static parameters => parameters.OctaveCount),
                        CreateNormalizationSettingsCodec()
                            .ForGetter<Parameters>(static parameters =>
                                new NormalizationSettings(
                                    parameters.Normalize,
                                    parameters.AmplitudeModifiers)))
                    .Apply(
                        instance,
                        static (baseAmplitude, baseOctave, octaveCount, settings) =>
                            new Parameters(
                                baseAmplitude,
                                baseOctave,
                                octaveCount,
                                settings.Normalize,
                                settings.AmplitudeModifiers)))
            .Validate(static parameters =>
                parameters.AmplitudeModifiers.Count != 0 &&
                parameters.AmplitudeModifiers.Count != parameters.OctaveCount
                    ? DataResult<Parameters>.Error(
                        () => $"amplitude_modifiers had size {parameters.AmplitudeModifiers.Count}, but octave_count was {parameters.OctaveCount}")
                    : DataResult<Parameters>.Success(parameters));

        public double BaseAmplitude { get; }
        public int BaseOctave { get; }
        public int OctaveCount { get; }
        public Normalization Normalize { get; }
        public IReadOnlyList<double> AmplitudeModifiers { get; }

        public Parameters(
            double baseAmplitude,
            int baseOctave,
            int octaveCount,
            Normalization normalize,
            IReadOnlyList<double> amplitudeModifiers)
        {
            BaseAmplitude = baseAmplitude;
            BaseOctave = baseOctave;
            OctaveCount = octaveCount;
            Normalize = normalize;
            AmplitudeModifiers = Array.AsReadOnly(amplitudeModifiers.ToArray());
        }

        public bool Equals(Parameters? other) =>
            other is not null &&
            BaseAmplitude.Equals(other.BaseAmplitude) &&
            BaseOctave == other.BaseOctave &&
            OctaveCount == other.OctaveCount &&
            Normalize == other.Normalize &&
            AmplitudeModifiers.SequenceEqual(other.AmplitudeModifiers);

        public override bool Equals(object? obj) =>
            obj is Parameters other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(BaseAmplitude);
            hash.Add(BaseOctave);
            hash.Add(OctaveCount);
            hash.Add(Normalize);
            foreach (double amplitudeModifier in AmplitudeModifiers)
                hash.Add(amplitudeModifier);
            return hash.ToHashCode();
        }

        public override string ToString() =>
            $"Parameters[baseAmplitude={BaseAmplitude}, baseOctave={BaseOctave}, octaveCount={OctaveCount}, normalize={Normalize}, amplitudeModifiers=[{string.Join(", ", AmplitudeModifiers)}]]";
    }

    public enum Normalization
    {
        DISABLED,
        ENABLED,
        LEGACY
    }

    public sealed class Builder
    {
        private double _baseAmplitude = 1.0;
        private int _baseOctave;
        private int _octaveCount = 1;
        private Normalization _normalize = Normalization.ENABLED;
        private readonly List<double> _amplitudeModifiers = [];

        public Builder SetBaseAmplitude(double baseAmplitude)
        {
            _baseAmplitude = baseAmplitude;
            return this;
        }

        public Builder SetBaseOctave(int baseOctave)
        {
            _baseOctave = baseOctave;
            return this;
        }

        public Builder SetOctaveCount(int octaveCount)
        {
            if (_amplitudeModifiers.Count != 0)
                throw new ArgumentException("Cannot set octave count after setting amplitude modifier");

            _octaveCount = octaveCount;
            return this;
        }

        public Builder SetNormalize(bool normalize)
        {
            _normalize = normalize ? Normalization.ENABLED : Normalization.DISABLED;
            return this;
        }

        public Builder SetLegacyNormalization()
        {
            _normalize = Normalization.LEGACY;
            return this;
        }

        public Builder SetAmplitudeModifier(int index, double value)
        {
            if (index < 0 || index >= _octaveCount)
            {
                throw new ArgumentException(
                    $"{index} outside of octave range [0; {_octaveCount})");
            }

            if (_amplitudeModifiers.Count == 0)
            {
                for (int i = 0; i < _octaveCount; i++)
                    _amplitudeModifiers.Add(1.0);
            }

            _amplitudeModifiers[index] = value;
            return this;
        }

        public NormalNoise Build()
        {
            var parameters = new Parameters(
                _baseAmplitude,
                _baseOctave,
                _octaveCount,
                _normalize,
                _amplitudeModifiers);
            return new NormalNoise(parameters);
        }
    }
}
