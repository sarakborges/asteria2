using Asteria.Serialization;
using Asteria.World.LevelGen.DensityFunctions;

namespace Asteria.World.LevelGen;

/// <summary>
/// One density-function entry exposed by noise-settings generation diagnostics.
/// </summary>
public sealed record NoiseGeneratorDebugFunctionEntry(
    string Label,
    DensityFunction Function)
{
    public static Codec<NoiseGeneratorDebugFunctionEntry> CODEC { get; } =
        RecordCodecBuilder.Create<NoiseGeneratorDebugFunctionEntry>(instance =>
            instance.Group(
                    Codecs.STRING.FieldOf("label")
                        .ForGetter<NoiseGeneratorDebugFunctionEntry>(static entry => entry.Label),
                    DensityFunction.CODEC.FieldOf("function")
                        .ForGetter<NoiseGeneratorDebugFunctionEntry>(static entry => entry.Function))
                .Apply(
                    instance,
                    static (label, function) =>
                        new NoiseGeneratorDebugFunctionEntry(label, function)));
}

/// <summary>
/// Optional density-function diagnostics collected for chunk-generation statistics.
/// </summary>
public sealed record NoiseGeneratorDebugFunctions(
    IReadOnlyList<NoiseGeneratorDebugFunctionEntry> Functions)
{
    public static NoiseGeneratorDebugFunctions EMPTY { get; } =
        new(Array.Empty<NoiseGeneratorDebugFunctionEntry>());

    public static Codec<NoiseGeneratorDebugFunctions> CODEC { get; } =
        NoiseGeneratorDebugFunctionEntry.CODEC.ListOf().XMap(
            static functions => new NoiseGeneratorDebugFunctions(functions),
            static debugFunctions => debugFunctions.Functions);
}
