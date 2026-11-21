namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// Attachment orientation for blocks mounted to a floor, wall or ceiling.
/// Declaration order and serialized names follow the 26.3 state-property enum.
/// </summary>
public enum AttachFace
{
    FLOOR,
    WALL,
    CEILING
}

public static class AttachFaceExtensions
{
    public static string GetSerializedName(this AttachFace face) =>
        face switch
        {
            AttachFace.FLOOR => "floor",
            AttachFace.WALL => "wall",
            AttachFace.CEILING => "ceiling",
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, "Unknown attachment face.")
        };
}
