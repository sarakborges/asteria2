namespace Asteria.Content.Blocks;

/// <summary>
/// A block type definition. Individual placed-block states are a separate layer.
/// </summary>
public class Block
{
    public BlockProperties Properties { get; }

    public Block(BlockProperties properties)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }
}
