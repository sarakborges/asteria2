namespace Asteria.Content.Blocks;

/// <summary>
/// A block type definition. Its state definition precomputes every valid immutable block state.
/// </summary>
public class Block
{
    public BlockProperties Properties { get; }
    public StateDefinition StateDefinition { get; }
    public BlockState DefaultState => StateDefinition.DefaultState;

    public Block(
        BlockProperties properties,
        Action<StateDefinitionBuilder>? configureState = null)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));

        var builder = new StateDefinitionBuilder();
        configureState?.Invoke(builder);
        StateDefinition = builder.Build(this);
    }

    public override string ToString() => GetType().Name;
}
