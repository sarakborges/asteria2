namespace Asteria.Content.Registries;

/// <summary>
/// Registration lifecycle metadata equivalent to Mojang's serialization Lifecycle.
/// </summary>
public abstract class Lifecycle
{
    private sealed class StableLifecycle : Lifecycle;
    private sealed class ExperimentalLifecycle : Lifecycle;

    private static readonly Lifecycle StableValue = new StableLifecycle();
    private static readonly Lifecycle ExperimentalValue = new ExperimentalLifecycle();

    private Lifecycle()
    {
    }

    public static Lifecycle Stable() => StableValue;

    public static Lifecycle Experimental() => ExperimentalValue;

    public static Lifecycle Deprecated(int since) => new DeprecatedLifecycle(since);

    public Lifecycle Add(Lifecycle other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(this, ExperimentalValue) || ReferenceEquals(other, ExperimentalValue))
        {
            return ExperimentalValue;
        }

        if (this is DeprecatedLifecycle currentDeprecated)
        {
            if (other is DeprecatedLifecycle otherDeprecated &&
                otherDeprecated.Since < currentDeprecated.Since)
            {
                return other;
            }

            return this;
        }

        if (other is DeprecatedLifecycle)
        {
            return other;
        }

        return StableValue;
    }

    public sealed class DeprecatedLifecycle : Lifecycle
    {
        public int Since { get; }

        internal DeprecatedLifecycle(int since)
        {
            Since = since;
        }
    }
}
