namespace Asteria.Server.Level;

/// <summary>
/// A chunk ticket carrying a ticket type, its distance-manager level and remaining timeout.
/// Port of Minecraft Java 26.3 net.minecraft.server.level.Ticket.
/// </summary>
public sealed class Ticket
{
    public TicketType Type { get; }
    public int TicketLevel { get; }
    public long TicksLeft { get; private set; }

    public Ticket(TicketType type, int ticketLevel)
        : this(type, ticketLevel, GetTimeout(type))
    {
    }

    private static long GetTimeout(TicketType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.Timeout;
    }

    private Ticket(TicketType type, int ticketLevel, long ticksLeft)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        if (ticketLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(ticketLevel));

        TicketLevel = ticketLevel;
        TicksLeft = ticksLeft;
    }

    public void ResetTicksLeft() =>
        TicksLeft = Type.Timeout;

    public void DecreaseTicksLeft()
    {
        if (Type.HasTimeout())
            TicksLeft--;
    }

    public bool IsTimedOut() =>
        Type.HasTimeout() && TicksLeft < 0L;
}
