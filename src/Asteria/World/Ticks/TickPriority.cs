using Asteria.Serialization;

namespace Asteria.World.Ticks;

/// <summary>Tick execution priority, ordered from extremely high (-3) to extremely low (3).</summary>
public sealed class TickPriority : IComparable<TickPriority>
{
    public static readonly TickPriority EXTREMELY_HIGH = new(-3, nameof(EXTREMELY_HIGH));
    public static readonly TickPriority VERY_HIGH = new(-2, nameof(VERY_HIGH));
    public static readonly TickPriority HIGH = new(-1, nameof(HIGH));
    public static readonly TickPriority NORMAL = new(0, nameof(NORMAL));
    public static readonly TickPriority LOW = new(1, nameof(LOW));
    public static readonly TickPriority VERY_LOW = new(2, nameof(VERY_LOW));
    public static readonly TickPriority EXTREMELY_LOW = new(3, nameof(EXTREMELY_LOW));

    private static readonly TickPriority[] Values =
    {
        EXTREMELY_HIGH, VERY_HIGH, HIGH, NORMAL, LOW, VERY_LOW, EXTREMELY_LOW
    };

    public static Codec<TickPriority> CODEC { get; } =
        Codecs.INT.XMap(ByValue, static priority => priority.GetValue());

    private readonly int _value;
    private readonly string _name;

    private TickPriority(int value, string name)
    {
        _value = value;
        _name = name;
    }

    public static TickPriority ByValue(int value)
    {
        foreach (TickPriority priority in Values)
        {
            if (priority._value == value)
                return priority;
        }

        return value < EXTREMELY_HIGH._value ? EXTREMELY_HIGH : EXTREMELY_LOW;
    }

    public int GetValue() => _value;

    public int CompareTo(TickPriority? other) =>
        other is null ? 1 : _value.CompareTo(other._value);

    public override string ToString() => _name;
}
