namespace Asteria.Content.Storage;

internal interface IPaletteResize<T> where T : class
{
    int OnResize(int bits, T lastAddedValue);
}

internal sealed class NoResizePaletteResize<T> : IPaletteResize<T> where T : class
{
    public static NoResizePaletteResize<T> Instance { get; } = new();

    private NoResizePaletteResize()
    {
    }

    public int OnResize(int bits, T lastAddedValue) =>
        throw new InvalidOperationException(
            $"Palette unexpectedly requested a resize to {bits} bits while copying data.");
}
