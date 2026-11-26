namespace Asteria.Client.Rendering;

/// <summary>
/// Width and height of one frame in a sprite sheet, matching Minecraft Java 26.3 FrameSize.
/// </summary>
public sealed record FrameSize
{
    public int Width { get; }
    public int Height { get; }

    public FrameSize(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
    }
}
