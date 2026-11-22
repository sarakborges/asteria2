namespace Asteria.Util;

/// <summary>
/// Controls whether a block-state scan continues or stops after the current match.
/// </summary>
public enum Continuation
{
    Continue,
    Abort
}

public static class ContinuationExtensions
{
    public static bool ShouldAbort(this Continuation continuation) =>
        continuation == Continuation.Abort;

    public static Continuation AbortIf(bool abortIf) =>
        abortIf ? Continuation.Abort : Continuation.Continue;

    public static Continuation ContinueIf(bool continueIf) =>
        AbortIf(!continueIf);
}
