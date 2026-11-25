using Asteria.World.Level;

internal static class ChunkResultChecks
{
    public static void Run()
    {
        CheckSuccess();
        CheckFailure();
        CheckNullSuccessFallback();
    }

    private static void CheckSuccess()
    {
        ChunkResult<string> result = ChunkResult<string>.Of("chunk");

        Require(
            result.IsSuccess() &&
            result.OrElse("fallback") == "chunk" &&
            result.GetError() is null &&
            result.OrElseThrow(static () => new InvalidOperationException("unexpected failure")) == "chunk",
            "A successful ChunkResult must expose its value and no error.");

        bool consumerCalled = false;
        ChunkResult<string> returned = result.IfSuccess(value =>
        {
            consumerCalled = value == "chunk";
        });
        Require(
            consumerCalled && ReferenceEquals(returned, result),
            "ChunkResult.ifSuccess must invoke the consumer and return the same successful result.");

        ChunkResult<int> mapped = result.Map(static value => value.Length);
        Require(
            mapped.IsSuccess() &&
            mapped.OrElse(-1) == 5,
            "ChunkResult.map must transform a successful value into another successful result.");
    }

    private static void CheckFailure()
    {
        int supplierCalls = 0;
        ChunkResult<string> result = ChunkResult<string>.Error(() =>
        {
            supplierCalls++;
            return "unloaded chunk";
        });

        Require(
            !result.IsSuccess() &&
            supplierCalls == 0,
            "A failed ChunkResult must retain its error supplier without evaluating it during construction.");

        Require(
            result.OrElse("fallback") == "fallback" &&
            result.GetError() == "unloaded chunk" &&
            result.GetError() == "unloaded chunk" &&
            supplierCalls == 2,
            "A failed ChunkResult must return the fallback and evaluate its error supplier lazily on each getError call.");

        bool consumerCalled = false;
        Require(
            ReferenceEquals(result.IfSuccess(_ => consumerCalled = true), result) &&
            !consumerCalled,
            "ChunkResult.ifSuccess must leave failed results untouched.");

        ChunkResult<int> mapped = result.Map(static _ => 0);
        Require(
            !mapped.IsSuccess() &&
            mapped.GetError() == "unloaded chunk" &&
            supplierCalls == 3,
            "ChunkResult.map must preserve a failure without evaluating the mapper or eager error text.");

        bool suppliedExceptionThrown = false;
        try
        {
            _ = result.OrElseThrow(static () => new InvalidOperationException("expected chunk failure"));
        }
        catch (InvalidOperationException exception)
        {
            suppliedExceptionThrown = exception.Message == "expected chunk failure";
        }

        Require(
            suppliedExceptionThrown,
            "ChunkResult.orElseThrow must throw the exception supplied by a failed result.");
    }

    private static void CheckNullSuccessFallback()
    {
        ChunkResult<string?> result = ChunkResult<string?>.Of(null);

        Require(
            result.IsSuccess() &&
            result.OrElse("direct fallback") is null &&
            ChunkResult<string?>.OrElse(result, "static fallback") == "static fallback",
            "The static ChunkResult.orElse helper must use its fallback when a successful result contains null.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
