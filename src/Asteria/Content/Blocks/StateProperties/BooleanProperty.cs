namespace Asteria.Content.Blocks.StateProperties;

/// <summary>
/// A two-valued property whose possible values follow the reference true, false order.
/// </summary>
public sealed class BooleanProperty : StateProperty<bool>
{
    private static readonly IReadOnlyList<bool> Values =
        Array.AsReadOnly(new[] { true, false });

    public override IReadOnlyList<bool> PossibleValues => Values;
    public override int ValueCount => Values.Count;

    private BooleanProperty(string name) : base(name)
    {
    }

    public static BooleanProperty Create(string name) => new(name);

    public override int IndexOf(bool value) => value ? 0 : 1;

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
