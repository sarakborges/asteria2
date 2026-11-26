using Asteria.Content.Identifiers;

namespace Asteria.Client.Rendering;

/// <summary>
/// Sprite pixel content and per-face transparency inspection, ported from Minecraft Java 26.3.
/// Animation exists only when the source resource includes animation metadata.
/// GPU upload, mipmap generation and animation-time advancement remain in the rendering backend.
/// </summary>
public sealed class SpriteContents : IDisposable
{
    private readonly NativeImage _originalImage;
    private readonly int _frameRowSize;
    private readonly int _totalSheetFrameCount;
    private readonly IReadOnlyList<FrameInfo> _frames;
    private readonly IReadOnlyList<int> _uniqueFrames;
    private readonly bool _hasAnimatedTexture;
    private bool _disposed;

    public ResourceLocation Name { get; }
    public int Width { get; }
    public int Height { get; }
    public FrameSize FrameSize { get; }
    public NativeImage OriginalImage =>
        !_disposed
            ? _originalImage
            : throw new ObjectDisposedException(nameof(SpriteContents));
    public Transparency SpriteTransparency { get; }
    public bool IsAnimated => FrameCount > 1;
    public int FrameCount => _hasAnimatedTexture ? _frames.Count : 1;
    public IReadOnlyList<FrameInfo> Frames => _frames;
    public IReadOnlyList<int> UniqueFrames => _uniqueFrames;

    public SpriteContents(ResourceLocation name, FrameSize frameSize, NativeImage image)
        : this(name, frameSize, image, animationMetadataPresent: false, frames: null, defaultFrameTime: 1)
    {
    }

    /// <summary>
    /// When animation metadata exists and its frame list is absent, all complete sheet cells are
    /// used sequentially. An explicit empty frame list is distinct and disables animation.
    /// Invalid frame entries are dropped as in the 26.3 source.
    /// </summary>
    public SpriteContents(
        ResourceLocation name,
        FrameSize frameSize,
        NativeImage image,
        IEnumerable<FrameInfo>? frames,
        int defaultFrameTime)
        : this(name, frameSize, image, animationMetadataPresent: true, frames, defaultFrameTime)
    {
    }

    private SpriteContents(
        ResourceLocation name,
        FrameSize frameSize,
        NativeImage image,
        bool animationMetadataPresent,
        IEnumerable<FrameInfo>? frames,
        int defaultFrameTime)
    {
        ArgumentNullException.ThrowIfNull(frameSize);
        ArgumentNullException.ThrowIfNull(image);

        Name = name;
        FrameSize = frameSize;
        _originalImage = image;
        Width = frameSize.Width;
        Height = frameSize.Height;

        if (frameSize.Width > image.Width || frameSize.Height > image.Height)
        {
            throw new ArgumentException(
                $"Frame size {frameSize.Width}x{frameSize.Height} exceeds image size {image.Width}x{image.Height}.",
                nameof(frameSize));
        }

        _frameRowSize = image.Width / frameSize.Width;
        int frameColumnSize = image.Height / frameSize.Height;
        _totalSheetFrameCount = checked(_frameRowSize * frameColumnSize);
        if (_totalSheetFrameCount <= 0)
            throw new ArgumentException("Sprite image does not contain a complete frame.", nameof(image));

        if (!animationMetadataPresent)
        {
            _frames = Array.Empty<FrameInfo>();
            _uniqueFrames = new[] { 1 };
            _hasAnimatedTexture = false;
        }
        else
        {
            List<FrameInfo> framesToUse = frames?.ToList() ??
                Enumerable.Range(0, _totalSheetFrameCount)
                    .Select(index => new FrameInfo(index, defaultFrameTime))
                    .ToList();

            framesToUse.RemoveAll(frame =>
                frame.Time <= 0 ||
                frame.Index < 0 ||
                frame.Index >= _totalSheetFrameCount);

            _frames = framesToUse.AsReadOnly();
            _uniqueFrames = framesToUse
                .Select(static frame => frame.Index)
                .Distinct()
                .ToArray();
            _hasAnimatedTexture = framesToUse.Count > 1;
        }

        SpriteTransparency = image.ComputeTransparency();
    }

    public Transparency ComputeTransparency(float u0, float v0, float u1, float v1)
    {
        ThrowIfDisposed();

        if (SpriteTransparency.IsOpaque())
            return SpriteTransparency;

        if (u0 == 0.0F && v0 == 0.0F && u1 == 1.0F && v1 == 1.0F)
            return SpriteTransparency;

        int x0 = (int)MathF.Floor(u0 * Width);
        int y0 = (int)MathF.Floor(v0 * Height);
        int x1 = (int)MathF.Ceiling(u1 * Width);
        int y1 = (int)MathF.Ceiling(v1 * Height);

        if (!_hasAnimatedTexture)
            return _originalImage.ComputeTransparency(x0, y0, x1, y1);

        Transparency result = Transparency.NONE;
        foreach (int frameIndex in _uniqueFrames)
        {
            int frameX = GetFrameX(frameIndex);
            int frameY = GetFrameY(frameIndex);
            result = result.Or(_originalImage.ComputeTransparency(
                frameX + x0,
                frameY + y0,
                frameX + x1,
                frameY + y1));
        }

        return result;
    }

    /// <summary>
    /// The frame argument is a source-sheet frame index, as in Minecraft 26.3.
    /// </summary>
    public bool IsTransparent(int frame, int x, int y)
    {
        ThrowIfDisposed();
        int actualX = x;
        int actualY = y;
        if (_hasAnimatedTexture)
        {
            actualX += GetFrameX(frame);
            actualY += GetFrameY(frame);
        }

        return _originalImage.GetAlpha(actualX, actualY) == 0;
    }

    public int GetFrameX(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_totalSheetFrameCount)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        return frameIndex % _frameRowSize * Width;
    }

    public int GetFrameY(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_totalSheetFrameCount)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        return frameIndex / _frameRowSize * Height;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _originalImage.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SpriteContents));
    }

    public readonly record struct FrameInfo(int Index, int Time);
}
