namespace Asteria.Content.Blocks;

/// <summary>
/// Core physical and semantic properties owned by a block type.
/// Visual resources and block-state properties are intentionally separate.
/// </summary>
public sealed class BlockProperties
{
    public float DestroyTime { get; }
    public float ExplosionResistance { get; }
    public bool RequiresCorrectToolForDrops { get; }
    public float Friction { get; }
    public bool IsAir { get; }

    public BlockProperties(
        float destroyTime = 1.5f,
        float explosionResistance = 6.0f,
        bool requiresCorrectToolForDrops = false,
        float friction = 0.6f,
        bool isAir = false)
    {
        if (!float.IsFinite(destroyTime) || (destroyTime < 0.0f && destroyTime != -1.0f))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destroyTime), "Destroy time must be -1 or a finite non-negative value.");
        }

        if (!float.IsFinite(explosionResistance) || explosionResistance < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(explosionResistance), "Explosion resistance must be finite and non-negative.");
        }

        if (!float.IsFinite(friction) || friction < 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(friction), "Friction must be finite and non-negative.");
        }

        DestroyTime = destroyTime;
        ExplosionResistance = explosionResistance;
        RequiresCorrectToolForDrops = requiresCorrectToolForDrops;
        Friction = friction;
        IsAir = isAir;
    }

    public static BlockProperties Default => new();
}
