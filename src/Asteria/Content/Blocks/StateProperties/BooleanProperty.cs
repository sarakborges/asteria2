namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// A two-valued property whose possible values are always ordered false, true.
/// </summary>
public sealed class BooleanProperty : StateProperty<bool>
{
    private static readonly IReadOnlyList<bool> Values =
        Array.AsReadOnly(new[] { false, true });

    public override IReadOnlyList<bool> PossibleValues => Values;
    public override int ValueCount => Values.Count;

    private BooleanProperty(string name) : base(name)
    {
    }

    public static BooleanProperty Create(string name) => new(name);

    public override string GetName(bool value) => value ? "true" : "false";

    public override bool TryParse(string serializedValue, out bool value)
    {
        switch (serializedValue)
        {
            case "false":
                value = false;
                return true;
            case "true":
                value = true;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
