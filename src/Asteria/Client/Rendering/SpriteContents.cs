using Asteria.Content.Identifiers;

namespace Asteria.Client.Rendering;

/// <summary>
/// Sprite pixel content and per-face transparency inspection, ported from Minecraft Java 26.3.
/// GPU upload, mipmap generation and animation-time advancement remain in the atlas backend.
/// </summary>
public sealed class SpriteContents : IDisposable
{
    private readonly NativeImage _originalImage;
    private readonly int _frameRowSize;
    private readonly int _frameCount;
    private readonly IReadOnlyList<FrameInfo> _frames;
    private readonly IReadOnlyList<int> _uniqueFrames;
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
    public bool IsAnimated => _frames.Count > 1;
    public IReadOnlyList<FrameInfo> Frames => _frames;
    public IReadOnlyList<int> UniqueFrames => _uniqueFrames;

    public SpriteContents(
        ResourceLocation name,
        FrameSize frameSize,
        NativeImage image)
        : this(name, frameSize, image, frames: null, defaultFrameTime: 1)
    {
    }

    public SpriteContents(
        ResourceLocation name,
        FrameSize frameSize,
        NativeImage image,
        IEnumerable<FrameInfo>? frames,
        int defaultFrameTime)
    {
        ArgumentNullException.ThrowIfNull(frameSize);
        ArgumentNullException.ThrowIfNull(image);
        if (defaultFrameTime <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultFrameTime));

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
        int frameRows = image.Height / frameSize.Height;
        _frameCount = checked(_frameRowSize * frameRows);
        if (_frameCount <= 0)
            throw new ArgumentException("Sprite image does not contain a complete frame.", nameof(image));

        List<FrameInfo> parsedFrames = frames?.ToList() ??
            Enumerable.Range(0, _frameCount)
                .Select(index => new FrameInfo(index, defaultFrameTime))
                .ToList();

        if (parsedFrames.Count == 0)
            throw new ArgumentException("An animation frame list cannot be empty.", nameof(frames));

        foreach (FrameInfo frame in parsedFrames)
        {
            if (frame.Index < 0 || frame.Index >= _frameCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frames),
                    $"Frame index {frame.Index} is outside the sprite sheet's {_frameCount} frames.");
            }

            if (frame.Time <= 0)
                throw new ArgumentOutOfRangeException(nameof(frames), "Frame duration must be positive.");
        }

        _frames = parsedFrames.AsReadOnly();
        _uniqueFrames = parsedFrames
            .Select(static frame => frame.Index)
            .Distinct()
            .ToArray();

        SpriteTransparency = image.ComputeTransparency();
    }

    /// <summary>
    /// Returns the transparency of a normalized UV region across every unique animation frame.
    /// UV bounds are converted with floor for the minimum and ceiling for the maximum.
    /// </summary>
    public Transparency ComputeTransparency(float u0, float v0, float u1, float v1)
    {
        ThrowIfDisposed();

        if (SpriteTransparency.IsOpaque())
            return Transparency.NONE;

        if (u0 == 0.0F && v0 == 0.0F && u1 == 1.0F && v1 == 1.0F)
            return SpriteTransparency;

        int x0 = (int)MathF.Floor(u0 * Width);
        int y0 = (int)MathF.Floor(v0 * Height);
        int x1 = (int)MathF.Ceiling(u1 * Width);
        int y1 = (int)MathF.Ceiling(v1 * Height);

        Transparency result = Transparency.NONE;
        foreach (int frameIndex in _uniqueFrames)
        {
            int frameX = GetFrameX(frameIndex);
            int frameY = GetFrameY(frameIndex);
            result = result.Or(_originalImage.ComputeTransparency(
                x0 + frameX,
                y0 + frameY,
                x1 + frameX,
                y1 + frameY));

            if (result == Transparency.TRANSPARENT_AND_TRANSLUCENT)
                return result;
        }

        return result;
    }

    public bool IsTransparent(int frameSequenceIndex, int x, int y)
    {
        ThrowIfDisposed();
        if ((uint)frameSequenceIndex >= (uint)_frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameSequenceIndex));
        if ((uint)x >= (uint)Width)
            throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y));

        FrameInfo frame = _frames[frameSequenceIndex];
        return _originalImage.GetAlpha(
            GetFrameX(frame.Index) + x,
            GetFrameY(frame.Index) + y) == 0;
    }

    public int GetFrameX(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_frameCount)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        return frameIndex % _frameRowSize * Width;
    }

    public int GetFrameY(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_frameCount)
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
