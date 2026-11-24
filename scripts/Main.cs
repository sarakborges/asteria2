using Asteria.Content.Blocks;
using Asteria.Content.Chunks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.World;
using Asteria.World.Level;
using Godot;

namespace Asteria;

public partial class Main : Node3D
{
    private const long DevelopmentWorldSeed = 132456789L;

    public OverworldWorld? World { get; private set; }

    public override void _Ready()
    {
        RegistryBootstrap.Initialize();

        ResourceLocation airId = ResourceLocation.FromNamespaceAndPath("minecraft", "air");
        if (!ReferenceEquals(BuiltInRegistries.Block.GetValue(airId), Blocks.AIR))
            throw new InvalidOperationException("The built-in air block must be registered.");

        if (!BuiltInRegistries.AreFrozen || !RegistryBootstrap.IsInitialized)
            throw new InvalidOperationException("Built-in content registries must be frozen after bootstrap.");

        GD.Print(
            $"Content registries ready: {BuiltInRegistries.Block.Count} blocks, " +
            $"{Block.BlockStateRegistry.Count} block states.");

        World = new OverworldWorld(DevelopmentWorldSeed);
        ProtoChunk originChunk = World.GetChunk(ChunkPos.Zero);
        int nonEmptySections = originChunk.GetSections().Count(section => !section.HasOnlyAir);
        GD.Print(
            $"Overworld ready: seed={World.Seed}, chunk={originChunk.GetPos()}, " +
            $"sections={originChunk.GetSections().Length}, nonEmptySections={nonEmptySections}, " +
            $"status={originChunk.GetPersistedStatus().GetIndex()}");
    }
}
