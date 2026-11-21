namespace Asteria.Core;

/// <summary>Three-valued boolean value with an explicit default state.</summary>
public enum TriState
{
    TRUE,
    FALSE,
    DEFAULT
}

public static class TriStateExtensions
{
    public static TriState From(bool value) => value ? TriState.TRUE : TriState.FALSE;

    public static bool ToBoolean(this TriState value, bool defaultValue) =>
        value switch
        {
            TriState.TRUE => true,
            TriState.FALSE => false,
            TriState.DEFAULT => defaultValue,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown tri-state value.")
        };

    public static string GetSerializedName(this TriState value) =>
        value switch
        {
            TriState.TRUE => "true",
            TriState.FALSE => "false",
            TriState.DEFAULT => "default",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown tri-state value.")
        };
}
