namespace Asteria.Content.Materials;

/// <summary>
/// Block behavior when a piston attempts to move it.
/// </summary>
public enum PushReaction
{
    PUSH_PULL,
    PUSH,
    POPPED,
    IMMOVEABLE,
    IGNORE_ENTITY
}
