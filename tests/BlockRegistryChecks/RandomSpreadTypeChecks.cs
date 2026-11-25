using System.Text.Json;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.Structure.Placement;

internal static class RandomSpreadTypeChecks
{
    public static void Run()
    {
        CheckCanonicalValuesAndCodec();
        CheckLinearEvaluation();
        CheckTriangularEvaluation();
        CheckInvalidLimitPropagation();
    }

    private static void CheckCanonicalValuesAndCodec()
    {
        Require(
            RandomSpreadType.Values.Count == 2 &&
            ReferenceEquals(RandomSpreadType.Values[0], RandomSpreadType.LINEAR) &&
            ReferenceEquals(RandomSpreadType.Values[1], RandomSpreadType.TRIANGULAR) &&
            RandomSpreadType.LINEAR.GetSerializedName() == "linear" &&
            RandomSpreadType.TRIANGULAR.GetSerializedName() == "triangular",
            "RandomSpreadType must expose the vanilla singleton values and serialized identifiers.");

        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        JsonElement linear = RandomSpreadType.CODEC.EncodeStart(
            ops,
            RandomSpreadType.LINEAR).GetOrThrow();
        JsonElement triangular = RandomSpreadType.CODEC.EncodeStart(
            ops,
            RandomSpreadType.TRIANGULAR).GetOrThrow();

        Require(
            linear.GetString() == "linear" &&
            triangular.GetString() == "triangular" &&
            ReferenceEquals(
                RandomSpreadType.CODEC.Parse(ops, linear).GetOrThrow(),
                RandomSpreadType.LINEAR) &&
            ReferenceEquals(
                RandomSpreadType.CODEC.Parse(ops, triangular).GetOrThrow(),
                RandomSpreadType.TRIANGULAR),
            "The random spread type codec must round-trip the exact singleton values.");

        Require(
            RandomSpreadType.CODEC.Parse(ops, ops.CreateString("unknown")).IsError,
            "The random spread type codec must reject unregistered identifiers.");
    }

    private static void CheckLinearEvaluation()
    {
        var random = new SequenceRandomSource(7);

        int result = RandomSpreadType.LINEAR.Evaluate(random, 10);

        Require(
            result == 7 && random.Calls == 1,
            "LINEAR must consume exactly one bounded random integer and return it unchanged.");
    }

    private static void CheckTriangularEvaluation()
    {
        var random = new SequenceRandomSource(2, 9);

        int result = RandomSpreadType.TRIANGULAR.Evaluate(random, 10);

        Require(
            result == 5 && random.Calls == 2,
            "TRIANGULAR must consume two bounded random integers in order and return their integer average.");
    }

    private static void CheckInvalidLimitPropagation()
    {
        var random = new SequenceRandomSource(1);

        RequireThrows<ArgumentException>(
            () => RandomSpreadType.LINEAR.Evaluate(random, 0),
            "LINEAR must preserve the random source's failure for a non-positive bound.");
        Require(
            random.Calls == 0,
            "An invalid bound must fail before the random source produces a value.");
    }

    private sealed class SequenceRandomSource : LegacyRandomSource
    {
        private readonly Queue<int> _values;

        public int Calls { get; private set; }

        public SequenceRandomSource(params int[] values) : base(0L)
        {
            _values = new Queue<int>(values);
        }

        public override int NextInt(int bound)
        {
            if (bound <= 0)
                throw new ArgumentException("Bound must be positive", nameof(bound));

            Calls++;
            int value = _values.Dequeue();
            if (value < 0 || value >= bound)
                throw new InvalidOperationException(
                    $"Test value {value} is outside bound {bound}.");

            return value;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException RequireThrows<TException>(Action action, string message)
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

        throw new InvalidOperationException(message);
    }
}
