using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.World.Clocks;

/// <summary>Generic identifiers for clock positions that can be declared by timelines.</summary>
public static class ClockTimeMarkers
{
    public static RegistryKey<Registry<ClockTimeMarker>> ROOT_ID { get; } =
        RegistryKey<ClockTimeMarker>.CreateRegistryKey(
            ResourceLocation.WithDefaultNamespace("clock_time_marker"));

    public static RegistryKey<ClockTimeMarker> DAY { get; } = CreateKey("day");
    public static RegistryKey<ClockTimeMarker> NOON { get; } = CreateKey("noon");
    public static RegistryKey<ClockTimeMarker> NIGHT { get; } = CreateKey("night");
    public static RegistryKey<ClockTimeMarker> MIDNIGHT { get; } = CreateKey("midnight");

    private static RegistryKey<ClockTimeMarker> CreateKey(string name) =>
        RegistryKey<ClockTimeMarker>.Create(
            ROOT_ID, ResourceLocation.WithDefaultNamespace(name));
}
