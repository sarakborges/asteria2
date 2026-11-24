using Asteria.Content.Blocks;

namespace Asteria.World.LevelGen;

/// <summary>
/// Vanilla disabled-aquifer implementation: density alone decides whether the global picker
/// supplies a fluid state, and no fluid update is scheduled.
/// </summary>
internal sealed class DisabledAquifer : Aquifer
{
    private readonly Aquifer.FluidPicker _fluidRule;

    public DisabledAquifer(Aquifer.FluidPicker fluidRule) =>
        _fluidRule = fluidRule ?? throw new ArgumentNullException(nameof(fluidRule));

    public BlockState? ComputeSubstance(
        int blockX,
        int blockY,
        int blockZ,
        double density) =>
        density > 0.0
            ? null
            : _fluidRule(blockX, blockY, blockZ).At(blockY);

    public bool ShouldScheduleFluidUpdate() => false;
}
