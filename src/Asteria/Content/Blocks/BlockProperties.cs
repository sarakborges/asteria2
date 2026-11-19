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
        float destroyTime = 0.0f,
        float explosionResistance = 0.0f,
        bool requiresCorrectToolForDrops = false,
        float friction = 0.6f,
        bool isAir = false)
    {
        DestroyTime = destroyTime;
        ExplosionResistance = MathF.Max(0.0f, explosionResistance);
        RequiresCorrectToolForDrops = requiresCorrectToolForDrops;
        Friction = friction;
        IsAir = isAir;
    }

    public static BlockProperties Default => new();
}
