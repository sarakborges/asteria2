using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Server.Level;

internal static class TicketTypeChecks
{
    public static void Run()
    {
        Require(
            BuiltInRegistries.TicketType.Count == 4,
            "The local ticket registry must contain the four ticket kinds required by loading, simulation, forced loading and unknown persisted entries.");

        RequireRegistered("player_loading", TicketType.PLAYER_LOADING);
        RequireRegistered("player_simulation", TicketType.PLAYER_SIMULATION);
        RequireRegistered("forced", TicketType.FORCED);
        RequireRegistered("unknown", TicketType.UNKNOWN);

        Require(
            TicketType.PLAYER_LOADING == new TicketType(TicketType.NO_TIMEOUT, TicketType.FLAG_LOADING) &&
            TicketType.PLAYER_SIMULATION == new TicketType(
                TicketType.NO_TIMEOUT,
                TicketType.FLAG_SIMULATION | TicketType.FLAG_KEEP_DIMENSION_ACTIVE) &&
            TicketType.FORCED == new TicketType(
                TicketType.NO_TIMEOUT,
                TicketType.FLAG_PERSIST | TicketType.FLAG_LOADING |
                TicketType.FLAG_SIMULATION | TicketType.FLAG_KEEP_DIMENSION_ACTIVE) &&
            TicketType.UNKNOWN == new TicketType(
                1L,
                TicketType.FLAG_LOADING | TicketType.FLAG_CAN_EXPIRE_IF_UNLOADED),
            "Built-in ticket records must preserve the vanilla timeout and flag values.");

        Require(
            TicketType.PLAYER_LOADING.DoesLoad() &&
            !TicketType.PLAYER_LOADING.DoesSimulate() &&
            !TicketType.PLAYER_LOADING.Persist() &&
            !TicketType.PLAYER_LOADING.ShouldKeepDimensionActive() &&
            !TicketType.PLAYER_LOADING.CanExpireIfUnloaded() &&
            !TicketType.PLAYER_LOADING.HasTimeout(),
            "PLAYER_LOADING must carry only the loading flag and no timeout.");

        Require(
            !TicketType.PLAYER_SIMULATION.DoesLoad() &&
            TicketType.PLAYER_SIMULATION.DoesSimulate() &&
            TicketType.PLAYER_SIMULATION.ShouldKeepDimensionActive() &&
            !TicketType.PLAYER_SIMULATION.Persist() &&
            !TicketType.PLAYER_SIMULATION.HasTimeout(),
            "PLAYER_SIMULATION must preserve simulation and keep-dimension-active semantics.");

        Require(
            TicketType.FORCED.Persist() &&
            TicketType.FORCED.DoesLoad() &&
            TicketType.FORCED.DoesSimulate() &&
            TicketType.FORCED.ShouldKeepDimensionActive() &&
            !TicketType.FORCED.CanExpireIfUnloaded() &&
            !TicketType.FORCED.HasTimeout(),
            "FORCED tickets must persist, load, simulate and keep the dimension active.");

        Require(
            TicketType.UNKNOWN.DoesLoad() &&
            !TicketType.UNKNOWN.DoesSimulate() &&
            !TicketType.UNKNOWN.Persist() &&
            TicketType.UNKNOWN.CanExpireIfUnloaded() &&
            TicketType.UNKNOWN.HasTimeout(),
            "UNKNOWN tickets must load, expire when unloaded and retain their one-tick timeout.");

        Require(
            TicketType.FLAG_PERSIST == 1 &&
            TicketType.FLAG_LOADING == 2 &&
            TicketType.FLAG_SIMULATION == 4 &&
            TicketType.FLAG_KEEP_DIMENSION_ACTIVE == 8 &&
            TicketType.FLAG_CAN_EXPIRE_IF_UNLOADED == 16,
            "TicketType flag bit values must match Minecraft Java 26.3.");
    }

    private static void RequireRegistered(string path, TicketType expected)
    {
        ResourceLocation location = ResourceLocation.FromNamespaceAndPath("asteria", path);
        Require(
            ReferenceEquals(BuiltInRegistries.TicketType.GetValue(location), expected),
            $"The asteria:{path} ticket must resolve to its registered singleton instance.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
