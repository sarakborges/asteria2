using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.World.Clocks;

/// <summary>Built-in clock definitions available to local worlds.</summary>
public static class WorldClocks
{
    private static readonly RegistryKey<Registry<WorldClock>> WorldClockRegistryKey =
        RegistryKey<WorldClock>.CreateRegistryKey(
            ResourceLocation.WithDefaultNamespace("world_clock"));

    public static RegistryKey<WorldClock> OVERWORLD { get; } = CreateKey("overworld");
    public static RegistryKey<WorldClock> THE_END { get; } = CreateKey("the_end");

    public static void RegisterAll()
    {
        BuiltInRegistries.WorldClock.Register(
            OVERWORLD, new WorldClock(), RegistrationInfo.BuiltIn);
        BuiltInRegistries.WorldClock.Register(
            THE_END, new WorldClock(), RegistrationInfo.BuiltIn);
    }

    private static RegistryKey<WorldClock> CreateKey(string name) =>
        RegistryKey<WorldClock>.Create(
            WorldClockRegistryKey, ResourceLocation.WithDefaultNamespace(name));
}
