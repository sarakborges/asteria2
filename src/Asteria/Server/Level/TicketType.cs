using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Server.Level;

/// <summary>
/// Ticket timeout and behavior flags from Minecraft Java 26.3.
/// </summary>
public sealed record TicketType(long Timeout, int Flags)
{
    public const long NO_TIMEOUT = 0L;
    public const int FLAG_PERSIST = 1;
    public const int FLAG_LOADING = 2;
    public const int FLAG_SIMULATION = 4;
    public const int FLAG_KEEP_DIMENSION_ACTIVE = 8;
    public const int FLAG_CAN_EXPIRE_IF_UNLOADED = 16;

    public static TicketType PLAYER_SPAWN { get; private set; } = null!;
    public static TicketType SPAWN_SEARCH { get; private set; } = null!;
    public static TicketType DRAGON { get; private set; } = null!;
    public static TicketType PLAYER_LOADING { get; private set; } = null!;
    public static TicketType PLAYER_SIMULATION { get; private set; } = null!;
    public static TicketType FORCED { get; private set; } = null!;
    public static TicketType PORTAL { get; private set; } = null!;
    public static TicketType ENDER_PEARL { get; private set; } = null!;
    public static TicketType UNKNOWN { get; private set; } = null!;

    internal static void RegisterAll()
    {
        PLAYER_SPAWN = Register("player_spawn", 20L, FLAG_LOADING);
        SPAWN_SEARCH = Register("spawn_search", 1L, FLAG_LOADING);
        DRAGON = Register("dragon", NO_TIMEOUT, FLAG_LOADING | FLAG_SIMULATION);
        PLAYER_LOADING = Register("player_loading", NO_TIMEOUT, FLAG_LOADING);
        PLAYER_SIMULATION = Register(
            "player_simulation",
            NO_TIMEOUT,
            FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        FORCED = Register(
            "forced",
            NO_TIMEOUT,
            FLAG_PERSIST | FLAG_LOADING | FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        PORTAL = Register(
            "portal",
            300L,
            FLAG_PERSIST | FLAG_LOADING | FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        ENDER_PEARL = Register(
            "ender_pearl",
            40L,
            FLAG_LOADING | FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        UNKNOWN = Register(
            "unknown",
            1L,
            FLAG_LOADING | FLAG_CAN_EXPIRE_IF_UNLOADED);
    }

    public bool Persist() => (Flags & FLAG_PERSIST) != 0;
    public bool DoesLoad() => (Flags & FLAG_LOADING) != 0;
    public bool DoesSimulate() => (Flags & FLAG_SIMULATION) != 0;
    public bool ShouldKeepDimensionActive() => (Flags & FLAG_KEEP_DIMENSION_ACTIVE) != 0;
    public bool CanExpireIfUnloaded() => (Flags & FLAG_CAN_EXPIRE_IF_UNLOADED) != 0;
    public bool HasTimeout() => Timeout != NO_TIMEOUT;

    private static TicketType Register(string name, long timeout, int flags)
    {
        var ticketType = new TicketType(timeout, flags);
        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("minecraft", name);
        RegistryKey<TicketType> key = RegistryKey<TicketType>.Create(
            BuiltInRegistries.TicketType.Key,
            location);
        BuiltInRegistries.TicketType.Register(key, ticketType, RegistrationInfo.BuiltIn);
        return ticketType;
    }
}
