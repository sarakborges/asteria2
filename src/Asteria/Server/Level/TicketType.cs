using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.Server.Level;

/// <summary>
/// Defines the flags and timeout policy attached to a server chunk ticket.
/// Mirrors Minecraft Java 26.3 TicketType for Asteria's local chunk lifecycle.
/// </summary>
/// <remarks>
/// Only ticket kinds required by local loading, simulation, forced loading and
/// unknown persisted entries are registered. Spawn, dragon, portal and ender-pearl
/// ticket kinds belong to gameplay systems outside Asteria's current product scope.
/// </remarks>
public sealed record TicketType(long Timeout, int Flags)
{
    public const long NO_TIMEOUT = 0L;

    public const int FLAG_PERSIST = 1;
    public const int FLAG_LOADING = 2;
    public const int FLAG_SIMULATION = 4;
    public const int FLAG_KEEP_DIMENSION_ACTIVE = 8;
    public const int FLAG_CAN_EXPIRE_IF_UNLOADED = 16;

    public static TicketType PLAYER_LOADING { get; private set; } = null!;
    public static TicketType PLAYER_SIMULATION { get; private set; } = null!;
    public static TicketType FORCED { get; private set; } = null!;
    public static TicketType UNKNOWN { get; private set; } = null!;

    internal static void RegisterAll()
    {
        PLAYER_LOADING = Register("player_loading", NO_TIMEOUT, FLAG_LOADING);
        PLAYER_SIMULATION = Register(
            "player_simulation",
            NO_TIMEOUT,
            FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        FORCED = Register(
            "forced",
            NO_TIMEOUT,
            FLAG_PERSIST | FLAG_LOADING | FLAG_SIMULATION | FLAG_KEEP_DIMENSION_ACTIVE);
        UNKNOWN = Register(
            "unknown",
            1L,
            FLAG_LOADING | FLAG_CAN_EXPIRE_IF_UNLOADED);
    }

    public bool Persist() =>
        (Flags & FLAG_PERSIST) != 0;

    public bool DoesLoad() =>
        (Flags & FLAG_LOADING) != 0;

    public bool DoesSimulate() =>
        (Flags & FLAG_SIMULATION) != 0;

    public bool ShouldKeepDimensionActive() =>
        (Flags & FLAG_KEEP_DIMENSION_ACTIVE) != 0;

    public bool CanExpireIfUnloaded() =>
        (Flags & FLAG_CAN_EXPIRE_IF_UNLOADED) != 0;

    public bool HasTimeout() =>
        Timeout != NO_TIMEOUT;

    private static TicketType Register(string name, long timeout, int flags)
    {
        var ticketType = new TicketType(timeout, flags);
        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("asteria", name);
        RegistryKey<TicketType> key = RegistryKey<TicketType>.Create(
            BuiltInRegistries.TicketType.Key,
            location);
        BuiltInRegistries.TicketType.Register(key, ticketType, RegistrationInfo.BuiltIn);
        return ticketType;
    }
}
