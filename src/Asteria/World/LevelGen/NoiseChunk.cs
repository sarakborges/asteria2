using Asteria.Content.Identifiers;
using AquiferContract = Asteria.World.LevelGen.Aquifer;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.Util.Context;

namespace Asteria.World.LevelGen;

/// <summary>
/// Chunk-scoped density sampling workspace from Minecraft Java 26.3. It binds the
/// dimension's compiled samplers to one cache-enabled context and owns its buffer-pool lease.
/// </summary>
public sealed class NoiseChunk : IDisposable
{
    private readonly RandomState _randomState;
    private readonly DensityBufferPool _bufferPool;
    private readonly SamplerContext _samplerContext;
    private bool _closed;

    public DensityVolume Volume { get; }
    public AquiferContract Aquifer { get; }
    public DensitySamplerSet CachingSamplers { get; }
    public bool IsClosed => _closed;

    public NoiseChunk(
        RandomState randomState,
        Beardifier? beardifier,
        NoiseGeneratorSettings settings,
        AquiferContract.FluidPicker globalFluidPicker,
        IChunkBlender? blender,
        DensityVolume volume)
    {
        _randomState = randomState ??
            throw new ArgumentNullException(nameof(randomState));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(globalFluidPicker);
        Volume = volume ?? throw new ArgumentNullException(nameof(volume));

        _bufferPool = randomState.AcquireDensityBufferPool();
        SamplerContext? samplerContext = null;

        try
        {
            ContextMap.Builder userFields = ContextMap.CreateBuilder()
                .WithParameter(
                    Beardifier.CONTEXT_KEY,
                    (DensitySampler)(beardifier ?? Beardifier.EMPTY));

            if (blender is not null && !blender.IsEmpty)
            {
                BlenderOutputBuffer blendOutput = blender.BlendOffsetAndFactor(volume) ??
                    throw new InvalidOperationException(
                        "A non-empty chunk blender must supply per-volume alpha and offset samplers.");

                userFields
                    .WithParameter(DensityFunctionContextKeys.BLENDER, blender)
                    .WithParameter(
                        DensityFunctionContextKeys.BLEND_ALPHA,
                        blendOutput.CreateAlphaSampler())
                    .WithParameter(
                        DensityFunctionContextKeys.BLEND_OFFSET,
                        blendOutput.CreateOffsetSampler());
            }

            samplerContext = SamplerContext.CreateBuilder()
                .SetUserFields(userFields.Build())
                .UseBufferArena(_bufferPool)
                .EnableCaches()
                .Build();

            _samplerContext = samplerContext;
            CachingSamplers = randomState.SamplersWithContext(samplerContext);
            Aquifer = settings.Aquifers is AquiferContract.Config aquiferConfig
                ? aquiferConfig.Create(
                    CachingSamplers,
                    randomState.GetOrCreateRandomFactory(
                        ResourceLocation.WithDefaultNamespace("aquifer")),
                    volume,
                    globalFluidPicker)
                : AquiferContract.CreateDisabled(globalFluidPicker);
        }
        catch
        {
            samplerContext?.Dispose();
            randomState.ReleaseDensityBufferPool(_bufferPool);
            throw;
        }
    }

    public DensitySampler.Bound GetSampler(DensityFunction function)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(function);
        return CachingSamplers.Get(function);
    }

    /// <summary>
    /// Closes the per-chunk sampling context and returns all its scoped buffers to the
    /// RandomState-owned pool for reuse by later chunks.
    /// </summary>
    public void Close() => Dispose();

    public void Dispose()
    {
        if (_closed)
            return;

        _closed = true;
        try
        {
            _samplerContext.Dispose();
        }
        finally
        {
            _randomState.ReleaseDensityBufferPool(_bufferPool);
        }
    }

    private void ThrowIfClosed()
    {
        if (_closed)
            throw new ObjectDisposedException(nameof(NoiseChunk));
    }
}
