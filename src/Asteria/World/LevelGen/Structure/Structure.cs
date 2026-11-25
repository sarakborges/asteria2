using Asteria.Content.Biomes;
using Asteria.Content.Registries;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Shared definition settings and bounding-box adjustment for a structure.
/// Generation-point selection and placement are separate operations that require
/// the structure manager and template systems not yet ported into Asteria.
/// </summary>
public abstract class Structure
{
    protected readonly StructureSettings _settings;

    protected Structure(StructureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public StructureSettings Settings => _settings;

    public HolderSet<Biome> Biomes() =>
        _settings.Biomes;

    public GenerationStep.Decoration Step() =>
        _settings.Step;

    public TerrainAdjustment TerrainAdaptation() =>
        _settings.TerrainAdaptation;

    /// <summary>
    /// Inflates structure bounds by twelve blocks on all axes whenever terrain
    /// adaptation is not NONE, matching Minecraft Java 26.3 Structure.adjustBoundingBox.
    /// </summary>
    public BoundingBox AdjustBoundingBox(BoundingBox boundingBox)
    {
        ArgumentNullException.ThrowIfNull(boundingBox);

        return ReferenceEquals(TerrainAdaptation(), TerrainAdjustment.NONE)
            ? boundingBox
            : boundingBox.InflatedBy(12);
    }
}
