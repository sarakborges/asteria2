using Asteria.Content.Registries;
using Asteria.Serialization;

internal static class SerializationChecks
{
    public static void Run()
    {
        CheckSuccessAndErrorStates();
        CheckMapAndFlatMap();
        CheckPartialResults();
        CheckErrorAccumulation();
        CheckLifecyclePropagation();
    }

    private static void CheckSuccessAndErrorStates()
    {
        DataResult<int> success = DataResult<int>.Success(7, Lifecycle.Stable());
        Require(success.IsSuccess && !success.IsError && success.HasResultOrPartial,
            "A successful data result must expose a complete value.");
        Require(success.GetOrThrow() == 7 && success.GetPartialOrThrow() == 7,
            "Both throw helpers must return the complete success value.");
        Require(success.TryGetResult(out int value) && value == 7,
            "A success must expose its complete result.");

        DataResult<int> error = DataResult<int>.Error("invalid value");
        Require(error.IsError && !error.IsSuccess && !error.HasResultOrPartial,
            "An error without a partial value must not expose a result.");
        Require(!error.TryGetResult(out _) && !error.TryGetPartial(out _),
            "An error without a partial value must fail both result lookups.");
        Require(error.ErrorMessage == "invalid value",
            "An error must preserve its message.");
        Capture<InvalidOperationException>(() => error.GetOrThrow(),
            "GetOrThrow must reject an error.");
        Capture<InvalidOperationException>(() => error.GetPartialOrThrow(),
            "GetPartialOrThrow must reject an error without a partial value.");
    }

    private static void CheckMapAndFlatMap()
    {
        DataResult<int> source = DataResult<int>.Success(5, Lifecycle.Stable());
        DataResult<string> mapped = source.Map(value => value.ToString());
        Require(mapped.IsSuccess && mapped.GetOrThrow() == "5",
            "Map must transform complete values.");

        DataResult<int> flatMapped = source.FlatMap(value =>
            DataResult<int>.Success(value + 4, Lifecycle.Experimental()));
        Require(flatMapped.IsSuccess && flatMapped.GetOrThrow() == 9,
            "FlatMap must transform complete values.");
        Require(ReferenceEquals(flatMapped.Lifecycle, Lifecycle.Experimental()),
            "FlatMap must combine the lifecycle of both operations.");
    }

    private static void CheckPartialResults()
    {
        DataResult<int> error = DataResult<int>.Error(
            () => "clamped",
            4,
            Lifecycle.Experimental());
        Require(error.IsError && error.HasResultOrPartial &&
                error.TryGetPartial(out int partial) && partial == 4,
            "An error may carry a partial value.");
        Require(error.GetPartialOrThrow() == 4,
            "GetPartialOrThrow must return an available partial value.");

        DataResult<int> mapped = error.Map(value => value * 3);
        Require(mapped.IsError && mapped.TryGetPartial(out int mappedPartial) &&
                mappedPartial == 12 && mapped.ErrorMessage == "clamped",
            "Map must transform partial values without discarding their error.");

        DataResult<int> promoted = error.PromotePartial(message =>
            Require(message == "clamped", "PromotePartial must report the original error."));
        Require(promoted.IsSuccess && promoted.GetOrThrow() == 4,
            "PromotePartial must promote the partial value to a success.");

        DataResult<int> continued = error.FlatMap(value =>
            DataResult<int>.Success(value + 2, Lifecycle.Stable()));
        Require(continued.IsError && continued.TryGetPartial(out int continuedPartial) &&
                continuedPartial == 6 && continued.ErrorMessage == "clamped",
            "FlatMap must continue from partial values while retaining the original error.");
    }

    private static void CheckErrorAccumulation()
    {
        DataResult<int> first = DataResult<int>.Error("first", 2);
        DataResult<int> second = DataResult<int>.Error("second", 3);

        DataResult<int> combined = first.Apply2((left, right) => left + right, second);
        Require(combined.IsError && combined.ErrorMessage == "first; second" &&
                combined.TryGetPartial(out int partial) && partial == 5,
            "Combining two partial errors must accumulate messages and combine partial values.");

        DataResult<int> noPartial = DataResult<int>.Error("missing");
        DataResult<int> stillMissing = noPartial.FlatMap(value =>
            DataResult<int>.Success(value + 1));
        Require(stillMissing.IsError && !stillMissing.HasResultOrPartial &&
                stillMissing.ErrorMessage == "missing",
            "FlatMap must retain errors that have no partial value.");
    }

    private static void CheckLifecyclePropagation()
    {
        DataResult<int> stable = DataResult<int>.Success(1, Lifecycle.Stable());
        DataResult<int> experimental = DataResult<int>.Success(2, Lifecycle.Experimental());
        DataResult<int> combined = stable.Apply2((left, right) => left + right, experimental);
        Require(ReferenceEquals(combined.Lifecycle, Lifecycle.Experimental()),
            "Combining results must combine lifecycle markers.");

        DataResult<int> reset = combined.SetLifecycle(Lifecycle.Stable());
        Require(ReferenceEquals(reset.Lifecycle, Lifecycle.Stable()),
            "SetLifecycle must replace the lifecycle marker.");

        DataResult<int> accumulated = stable.AddLifecycle(Lifecycle.Experimental());
        Require(ReferenceEquals(accumulated.Lifecycle, Lifecycle.Experimental()),
            "AddLifecycle must use the lifecycle monoid.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(message, exception);
        }

        throw new InvalidOperationException(message);
    }
}
