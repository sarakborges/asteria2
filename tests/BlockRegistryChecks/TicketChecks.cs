using Asteria.Server.Level;

internal static class TicketChecks
{
    public static void Run()
    {
        CheckConstruction();
        CheckTimeoutLifecycle();
        CheckNoTimeoutLifecycle();
        CheckIndependentInstances();
        CheckInvalidArguments();
    }

    private static void CheckConstruction()
    {
        var ticket = new Ticket(TicketType.PLAYER_LOADING, 33);
        Require(ReferenceEquals(ticket.Type, TicketType.PLAYER_LOADING), "Ticket must retain its exact registered type instance.");
        Require(ticket.TicketLevel == 33, "Ticket must retain its distance-manager level.");
        Require(ticket.TicksLeft == TicketType.PLAYER_LOADING.Timeout, "Ticket must initialize its timeout from its type.");
        Require(!ticket.IsTimedOut(), "A ticket without timeout must never time out.");
    }

    private static void CheckTimeoutLifecycle()
    {
        var ticket = new Ticket(TicketType.UNKNOWN, 44);
        Require(ticket.TicksLeft == 1L, "UNKNOWN ticket must start with its one-tick timeout.");
        ticket.DecreaseTicksLeft();
        Require(ticket.TicksLeft == 0L && !ticket.IsTimedOut(), "A ticket times out only after its remaining ticks become negative.");
        ticket.DecreaseTicksLeft();
        Require(ticket.TicksLeft == -1L && ticket.IsTimedOut(), "A ticket must time out below zero.");
        ticket.ResetTicksLeft();
        Require(ticket.TicksLeft == 1L && !ticket.IsTimedOut(), "Reset must restore the type timeout and clear timed-out state.");
    }

    private static void CheckNoTimeoutLifecycle()
    {
        var ticket = new Ticket(TicketType.FORCED, 33);
        for (int i = 0; i < 5; i++)
            ticket.DecreaseTicksLeft();

        Require(ticket.TicksLeft == TicketType.NO_TIMEOUT, "Decreasing a no-timeout ticket must leave its tick counter unchanged.");
        Require(!ticket.IsTimedOut(), "A no-timeout ticket must remain active.");
        ticket.ResetTicksLeft();
        Require(ticket.TicksLeft == TicketType.NO_TIMEOUT, "Resetting a no-timeout ticket must preserve zero.");
    }

    private static void CheckIndependentInstances()
    {
        var first = new Ticket(TicketType.UNKNOWN, 44);
        var second = new Ticket(TicketType.UNKNOWN, 45);
        first.DecreaseTicksLeft();
        first.DecreaseTicksLeft();

        Require(first.IsTimedOut(), "The first ticket must independently expire.");
        Require(second.TicksLeft == 1L && !second.IsTimedOut(), "Changing one ticket must not mutate another ticket.");
    }

    private static void CheckInvalidArguments()
    {
        RequireThrows<ArgumentNullException>(
            () => new Ticket(null!, 33),
            "Ticket must reject a null type.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => new Ticket(TicketType.PLAYER_LOADING, -1),
            "Ticket levels cannot be negative.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
