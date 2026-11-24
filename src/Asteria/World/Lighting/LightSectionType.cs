namespace Asteria.World.Lighting;

/// <summary>
/// Describes whether a light section is absent, contains only propagated light,
/// or contains both block data and light storage.
/// </summary>
public enum LightSectionType
{
    Empty,
    LightOnly,
    LightAndData
}
