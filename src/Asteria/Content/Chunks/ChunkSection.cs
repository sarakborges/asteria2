using Asteria.Content.Blocks;
using Asteria.Content.Biomes;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Content.Storage;

namespace Asteria.Content.Chunks;

/// <summary>
/// A 16x16x16 block-state section paired with the vanilla 4x4x4 quart-resolution biome palette.
/// </summary>
public class ChunkSection
{
    public const int Width = 16;
    public const int Height = 16;
    public const int Depth = 16;
    public const int BlockCount = Width * Height * Depth;
    public const int BiomeContainerBits = 2;
    public const int BiomeContainerWidth = 1 << BiomeContainerBits;
    public const int BiomeCount = BiomeContainerWidth * BiomeContainerWidth * BiomeContainerWidth;

    private readonly PalettedContainer<BlockState> _states;
    private IPalettedContainerReadOnly<Holder<Biome>> _biomes;
    private short _nonAirBlockCount;
    private short _fluidCount;
    private short _tickingBlockCount;
    private short _tickingFluidCount;

    public bool HasOnlyAir => _nonAirBlockCount == 0;
    public bool HasFluid => _fluidCount > 0;
    public bool IsRandomlyTickingBlocks => _tickingBlockCount > 0;
    public bool IsRandomlyTickingFluids => _tickingFluidCount > 0;
    public bool IsRandomlyTicking => IsRandomlyTickingBlocks || IsRandomlyTickingFluids;
    public ChunkSection(
        PalettedContainer<BlockState> states,
        IPalettedContainerReadOnly<Holder<Biome>> biomes)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));
        _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
        RecalculateBlockCounts();
    }

    private ChunkSection(
        PalettedContainer<BlockState> states,
        PalettedContainer<Holder<Biome>> biomes,
        short nonAirBlockCount,
        short fluidCount,
        short tickingBlockCount,
        short tickingFluidCount)
    {
        _states = states;
        _biomes = biomes;
        _nonAirBlockCount = nonAirBlockCount;
        _fluidCount = fluidCount;
        _tickingBlockCount = tickingBlockCount;
        _tickingFluidCount = tickingFluidCount;
    }

    public BlockState GetBlockState(int x, int y, int z) =>
        _states.Get(x, y, z);

    public FluidState GetFluidState(int x, int y, int z) =>
        GetBlockState(x, y, z).GetFluidState();

    public BlockState SetBlockState(int x, int y, int z, BlockState state) =>
        SetBlockState(x, y, z, state, true);

    public BlockState SetBlockState(int x, int y, int z, BlockState state, bool checkThreading)
    {
        ArgumentNullException.ThrowIfNull(state);

        BlockState previous = checkThreading
            ? _states.GetAndSet(x, y, z, state)
            : _states.GetAndSetUnchecked(x, y, z, state);

        if (!previous.IsAir)
        {
            _nonAirBlockCount = unchecked((short)(_nonAirBlockCount - 1));
            if (previous.IsRandomlyTicking)
            {
                _tickingBlockCount = unchecked((short)(_tickingBlockCount - 1));
            }

            FluidState previousFluid = previous.GetFluidState();
            if (!previousFluid.IsEmpty())
            {
                _fluidCount = unchecked((short)(_fluidCount - 1));
                if (previousFluid.IsRandomlyTicking())
                    _tickingFluidCount = unchecked((short)(_tickingFluidCount - 1));
            }
        }

        if (!state.IsAir)
        {
            _nonAirBlockCount = unchecked((short)(_nonAirBlockCount + 1));
            if (state.IsRandomlyTicking)
            {
                _tickingBlockCount = unchecked((short)(_tickingBlockCount + 1));
            }

            FluidState fluid = state.GetFluidState();
            if (!fluid.IsEmpty())
            {
                _fluidCount = unchecked((short)(_fluidCount + 1));
                if (fluid.IsRandomlyTicking())
                    _tickingFluidCount = unchecked((short)(_tickingFluidCount + 1));
            }
        }

        return previous;
    }

    public void Acquire() => _states.Acquire();

    public void Release() => _states.Release();

    public bool MaybeHas(Func<BlockState, bool> predicate) =>
        _states.MaybeHas(predicate);

    /// <summary>Returns the mutable block-state container, matching LevelChunkSection.getStates().</summary>
    public PalettedContainer<BlockState> GetStates() => _states;

    /// <summary>Returns biome palette storage through the read-only vanilla container contract.</summary>
    public IPalettedContainerReadOnly<Holder<Biome>> GetBiomes() => _biomes;

    public Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ) =>
        _biomes.Get(quartX, quartY, quartZ);

    /// <summary>
    /// Replaces all 64 quart-resolution biome entries using world quart coordinates.
    /// The palette is recreated before filling, matching LevelChunkSection.fillBiomesFromNoise.
    /// </summary>
    public void FillBiomesFromNoise(
        BiomeResolver biomeResolver,
        int quartMinX,
        int quartMinY,
        int quartMinZ)
    {
        ArgumentNullException.ThrowIfNull(biomeResolver);
        PalettedContainer<Holder<Biome>> newBiomes = _biomes.Recreate();

        for (int x = 0; x < BiomeContainerWidth; x++)
        for (int y = 0; y < BiomeContainerWidth; y++)
        for (int z = 0; z < BiomeContainerWidth; z++)
        {
            Holder<Biome> biome = biomeResolver.GetNoiseBiome(
                quartMinX + x,
                quartMinY + y,
                quartMinZ + z);
            newBiomes.GetAndSetUnchecked(x, y, z, biome);
        }

        _biomes = newBiomes;
    }

    /// <summary>Rebuilds cached section counts from the block-state palette.</summary>
    public void RecalculateBlockCounts()
    {
        int nonAirBlockCount = 0;
        int fluidCount = 0;
        int tickingBlockCount = 0;
        int tickingFluidCount = 0;
        _states.Count((state, count) =>
        {
            if (!state.IsAir)
            {
                nonAirBlockCount += count;
                if (state.IsRandomlyTicking)
                    tickingBlockCount += count;

                FluidState fluid = state.GetFluidState();
                if (!fluid.IsEmpty())
                {
                    fluidCount += count;
                    if (fluid.IsRandomlyTicking())
                        tickingFluidCount += count;
                }
            }
        });

        _nonAirBlockCount = unchecked((short)nonAirBlockCount);
        _fluidCount = unchecked((short)fluidCount);
        _tickingBlockCount = unchecked((short)tickingBlockCount);
        _tickingFluidCount = unchecked((short)tickingFluidCount);
    }

    public ChunkSection Copy() =>
        new(
            _states.Copy(),
            _biomes.Copy(),
            _nonAirBlockCount,
            _fluidCount,
            _tickingBlockCount,
            _tickingFluidCount);

}
