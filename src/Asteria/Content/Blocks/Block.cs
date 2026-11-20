using Asteria.Content.Registries;

namespace Asteria.Content.Blocks;

/// <summary>
/// A block type definition. Its state definition precomputes every valid immutable block state.
/// </summary>
public class Block
{
    /// <summary>Global block-state ID map, populated as each state is constructed.</summary>
    public static BlockStateIdMap BlockStateRegistry { get; } = new();

    public BlockProperties Properties { get; }
    public StateDefinition StateDefinition { get; }
    public float ExplosionResistance { get; }
    public float Friction { get; }
    public BlockState DefaultState { get; private set; } = null!;
    public Holder<Block>.Reference? BuiltInRegistryHolder { get; }

    public Block(BlockProperties properties)
        : this(properties, createIntrusiveRegistryHolder: true, configureState: null)
    {
    }

    protected Block(
        BlockProperties properties,
        bool createIntrusiveRegistryHolder,
        Action<StateDefinitionBuilder>? configureState = null)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        ExplosionResistance = Properties.ExplosionResistanceValue;
        Friction = Properties.FrictionValue;

        // Minecraft creates the intrusive holder as a Block field initializer,
        // before the constructor materializes the state definition.
        if (createIntrusiveRegistryHolder)
        {
            BuiltInRegistryHolder = BuiltInRegistries.Block.CreateIntrusiveHolder(this);
        }

        var builder = new StateDefinitionBuilder(this);
        CreateBlockStateDefinition(builder);
        configureState?.Invoke(builder);
        StateDefinition = builder.Build(this);
        DefaultState = StateDefinition.Any();
    }

    /// <summary>
    /// Equivalent to Block.createBlockStateDefinition: subclasses declare their state properties here.
    /// </summary>
    protected virtual void CreateBlockStateDefinition(StateDefinitionBuilder builder)
    {
    }

    protected void RegisterDefaultState(BlockState state)
    {
        DefaultState = state;
    }

    public override string ToString() =>
        $"Block{{{BuiltInRegistryHolder?.KeyOrNull?.Location.ToString() ?? "[unregistered]"}}}";
}
