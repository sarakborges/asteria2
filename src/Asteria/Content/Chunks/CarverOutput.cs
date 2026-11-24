namespace Asteria.Content.Chunks;

/// <summary>
/// Receives carved block coordinates and exposes the inclusive vertical bounds
/// available to a world-generation carver. Port of Minecraft Java 26.3 CarverOutput.
/// </summary>
public interface CarverOutput
{
    void Carve(int x, int y, int z);

    int MinY();

    int MaxY();
}
