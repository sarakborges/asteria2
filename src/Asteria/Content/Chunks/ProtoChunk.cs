using Asteria.Content.Blocks;
using Asteria.Content.Fluids;
using Asteria.Content.Registries;
using Asteria.Content.Chunks.Status;
using Asteria.Core;
using Asteria.World;
using Asteria.World.Lighting;
using BuiltInBlocks = Asteria.Content.Blocks.Blocks;
using BuiltInFluids = Asteria.Content.Fluids.Fluids;

namespace Asteria.Content.Chunks;

/// <summary>
/// Mutable chunk storage used while world generation is in progress.
/// This slice ports the block-state, lighting-notification and heightmap path of Minecraft Java 26.3 ProtoChunk.
/// </summary>
public sealed class ProtoChunk : ChunkAccess
{
    private volatile LightEngine? _lightEngine;
    private volatile ChunkStatus _status = ChunkStatus.EMPTY;

    public ProtoChunk(
        ChunkPos chunkPos,
        LevelHeightAccessor levelHeightAccessor,
        PalettedContainerFactory containerFactory,
        ChunkSection?[]? sections = null)
        : base(chunkPos, levelHeightAccessor, containerFactory, inhabitedTime: 0, sections)
    {
    }

    public override ChunkStatus GetPersistedStatus() => _status;

    public void SetPersistedStatus(ChunkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;
        MarkUnsaved();
    }

    public void SetLightEngine(LightEngine lightEngine) =>
        _lightEngine = lightEngine ?? throw new ArgumentNullException(nameof(lightEngine));

    public override BlockState GetBlockState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int y = position.GetY();
        if (IsOutsideBuildHeight(y))
            return BuiltInBlocks.VOID_AIR.DefaultState;

        ChunkSection section = GetSection(GetSectionIndex(y));
        if (section.HasOnlyAir)
            return BuiltInBlocks.AIR.DefaultState;

        return section.GetBlockState(
            position.GetX() & SectionPos.SectionMask,
            y & SectionPos.SectionMask,
            position.GetZ() & SectionPos.SectionMask);
    }

    public override FluidState GetFluidState(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int y = position.GetY();
        if (IsOutsideBuildHeight(y))
            return BuiltInFluids.EMPTY.DefaultFluidState;

        ChunkSection section = GetSection(GetSectionIndex(y));
        if (section.HasOnlyAir)
            return BuiltInFluids.EMPTY.DefaultFluidState;

        return section.GetFluidState(
            position.GetX() & SectionPos.SectionMask,
            y & SectionPos.SectionMask,
            position.GetZ() & SectionPos.SectionMask);
    }

    public BlockState SetBlockState(BlockPos position, BlockState state) =>
        SetBlockState(position, state, flags: 3);

    /// <summary>
    /// The update flags are accepted for API parity; ProtoChunk itself does not dispatch world neighbor updates.
    /// </summary>
    public BlockState SetBlockState(BlockPos position, BlockState state, int flags)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(state);
        _ = flags;

        int x = position.GetX();
        int y = position.GetY();
        int z = position.GetZ();
        if (IsOutsideBuildHeight(y))
            return BuiltInBlocks.VOID_AIR.DefaultState;

        int sectionIndex = GetSectionIndex(y);
        ChunkSection section = GetSection(sectionIndex);
        bool wasEmpty = section.HasOnlyAir;
        if (wasEmpty && ReferenceEquals(state.Block, BuiltInBlocks.AIR))
            return state;

        int localX = SectionPos.SectionRelative(x);
        int localY = SectionPos.SectionRelative(y);
        int localZ = SectionPos.SectionRelative(z);
        BlockState oldState = section.SetBlockState(localX, localY, localZ, state);

        if (_status.IsOrAfter(ChunkStatus.INITIALIZE_LIGHT))
        {
            bool isEmpty = section.HasOnlyAir;
            if (isEmpty != wasEmpty)
                _lightEngine!.UpdateSectionStatus(SectionPos.Of(position), isEmpty);

            if (LightEngine.HasDifferentLightProperties(oldState, state))
            {
                GetSkyLightSources().Update(this, localX, y, localZ);
                _lightEngine!.CheckBlock(position);
            }
        }

        ISet<Heightmap.Types> heightmapsAfter = _status.GetHeightmapsAfter();
        HashSet<Heightmap.Types>? toPrime = null;
        foreach (Heightmap.Types type in heightmapsAfter)
        {
            if (GetHeightmaps().FirstOrDefault(entry => ReferenceEquals(entry.Key, type)).Value is not { } heightmap)
            {
                toPrime ??= new HashSet<Heightmap.Types>();
                toPrime.Add(type);
                continue;
            }

            heightmap.Update(localX, y, localZ, state);
        }

        if (toPrime is not null)
            Heightmap.PrimeHeightmaps(this, toPrime);

        return oldState;
    }
}
