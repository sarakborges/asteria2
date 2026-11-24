using Asteria.Core;
using Asteria.Util.Context;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Evaluation context for density samplers, including vanilla point and volume caches.
/// </summary>
public sealed class SamplerContext : IDisposable
{
    public static readonly SamplerContext EmptyUncached =
        new(ContextMap.Empty, DensityBufferArena.Global, enableCaches: false, isShared: true);

    private const int CacheSizeStep = 16;

    private readonly ContextMap _userFields;
    private readonly DensityBufferArena _bufferArena;
    private CacheCell[]? _cacheCells;
    private readonly bool _isShared;
    private bool _disposed;

    private SamplerContext(ContextMap userFields, DensityBufferArena bufferArena, bool enableCaches)
    {
        _userFields = userFields ?? throw new ArgumentNullException(nameof(userFields));
        _bufferArena = bufferArena ?? throw new ArgumentNullException(nameof(bufferArena));
        _cacheCells = enableCaches ? Array.Empty<CacheCell>() : null;
        _isShared = isShared;
    }

    public static Builder CreateBuilder() => new();

    public T? GetField<T>(ContextKey<T> key) =>
        _userFields.Get(key);

    public T GetFieldOrDefault<T>(ContextKey<T> key, T defaultValue) =>
        _userFields.GetOrDefault(key, defaultValue)!;

    public ScopedDensityBuffer AcquireBuffer(DensityVolume volume)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(volume);
        return _bufferArena.Acquire(volume.Size());
    }

    private CacheCell? GetCacheCell(int cacheId)
    {
        ThrowIfDisposed();
        if (_cacheCells is null)
            return null;

        int oldSize = _cacheCells.Length;
        if (cacheId >= oldSize)
        {
            int newSize = MthRoundToward(unchecked(cacheId + 1), CacheSizeStep);
            Array.Resize(ref _cacheCells, newSize);
            for (int index = oldSize; index < newSize; index++)
                _cacheCells[index] = new CacheCell();
        }

        return _cacheCells[cacheId];
    }

    internal void SampleVolumeCached(
        int cacheId,
        DensitySampler input,
        DensityBuffer outputBuffer,
        DensityVolume volume)
    {
        CacheCell? cell = GetCacheCell(cacheId);
        if (cell is null)
        {
            input.SampleVolume(this, outputBuffer, volume);
        }
        else
        {
            if (cell.Buffer is null || !volume.Equals(cell.Volume))
            {
                cell.Buffer?.Close();
                cell.Volume = volume;
                cell.Buffer = AcquireBuffer(volume);
                input.SampleVolume(this, cell.Buffer, volume);
            }

            outputBuffer.CopyFrom(cell.Buffer);
        }
    }

    internal float SampleValueCached(
        int cacheId,
        DensitySampler input,
        int blockX,
        int blockY,
        int blockZ)
    {
        CacheCell? cell = GetCacheCell(cacheId);
        if (cell is null)
            return input.SampleValue(this, blockX, blockY, blockZ);

        long cacheKey = BlockPos.AsLong(blockX, blockY, blockZ);
        if (cell.ValueKey == cacheKey && !float.IsNaN(cell.Value))
            return cell.Value;

        if (cell.Buffer is not null && cell.Volume is not null)
        {
            int index = cell.Volume.IndexOfBlock(blockX, blockY, blockZ);
            if (index != DensityVolume.NO_BLOCK)
                return cell.Buffer.Get(index);
        }

        float value = input.SampleValue(this, blockX, blockY, blockZ);
        cell.ValueKey = cacheKey;
        cell.Value = value;
        return value;
    }

    private static int MthRoundToward(int input, int multiple) =>
        unchecked((input + multiple - 1) / multiple * multiple);

    private sealed class CacheCell
    {
        public DensityVolume? Volume;
        public ScopedDensityBuffer? Buffer;
        public long ValueKey;
        public float Value = float.NaN;
    }

    public void Dispose()
    {
        if (_isShared || _disposed)
            return;

        _disposed = true;
        if (_cacheCells is null)
            return;

        foreach (CacheCell cell in _cacheCells)
        {
            cell.Buffer?.Close();
            cell.Buffer = null;
            cell.Volume = null;
            cell.Value = float.NaN;
            cell.ValueKey = 0L;
        }

        _cacheCells = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SamplerContext));
    }

    public sealed class Builder
    {
        private ContextMap _userFields = ContextMap.Empty;
        private DensityBufferArena _bufferArena = DensityBufferArena.Global;
        private bool _enableCaches;

        public Builder SetUserFields(ContextMap userFields)
        {
            _userFields = userFields ?? throw new ArgumentNullException(nameof(userFields));
            return this;
        }

        public Builder UseBufferArena(DensityBufferArena bufferArena)
        {
            _bufferArena = bufferArena ?? throw new ArgumentNullException(nameof(bufferArena));
            return this;
        }

        public Builder EnableCaches()
        {
            _enableCaches = true;
            return this;
        }

        public SamplerContext Build() =>
            new(_userFields, _bufferArena, _enableCaches);
    }
}
