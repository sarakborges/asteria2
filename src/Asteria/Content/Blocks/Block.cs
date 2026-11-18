using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// A block type definition. Its state definition precomputes every valid immutable block state.
/// </summary>
public class Block
{
    public BlockProperties Properties { get; }
    public StateDefinition StateDefinition { get; }
    public BlockState DefaultState => StateDefinition.DefaultState;
    public Holder<Block>.Reference? BuiltInRegistryHolder { get; }

    public Block(
        BlockProperties properties,
        Action<StateDefinitionBuilder>? configureState = null,
        bool createIntrusiveRegistryHolder = false)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));

        var builder = new StateDefinitionBuilder();
        configureState?.Invoke(builder);
        StateDefinition = builder.Build(this);

        if (createIntrusiveRegistryHolder)
        {
            BuiltInRegistryHolder = BuiltInRegistries.Block.CreateIntrusiveHolder(this);
        }
    }

    public override string ToString() => GetType().Name;
}
