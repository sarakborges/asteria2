using Asteria.Content.Chunks;

internal static class DataLayerChecks
{
    public static void Run()
    {
        CheckLazyDefaultAndFill();
        CheckCoordinateAndNibbleLayout();
        CheckArrayOwnershipAndCopy();
        CheckFormattingAndEmptyState();
        CheckInvalidDataLength();
    }

    private static void CheckLazyDefaultAndFill()
    {
        var empty = new DataLayer();
        Require(empty.IsDefinitelyHomogenous(), "A fresh DataLayer must remain lazily homogeneous.");
        Require(empty.IsDefinitelyFilledWith(0), "A fresh DataLayer must represent zero without allocating bytes.");
        Require(empty.IsEmpty(), "A fresh zero-filled DataLayer must be empty.");
        Require(empty.Get(15, 15, 15) == 0, "A homogeneous DataLayer must return its default value.");

        var filled = new DataLayer(7);
        Require(filled.IsDefinitelyFilledWith(7), "The constructor default must be retained.");
        Require(filled.Get(0, 0, 0) == 7 && filled.Get(15, 15, 15) == 7,
            "A homogeneous DataLayer must return its default at every coordinate.");

        byte[] bytes = filled.GetData();
        Require(bytes.Length == DataLayer.Size, "Materialized light data must contain 2048 bytes.");
        Require(bytes.All(value => value == 0x77), "A filled DataLayer must materialize both nibbles in every byte.");
        Require(!filled.IsDefinitelyHomogenous(), "Materializing the backing array must end the homogeneous representation.");

        filled.Fill(3);
        Require(filled.IsDefinitelyFilledWith(3) && filled.Get(4, 8, 12) == 3,
            "Fill must discard the previous byte array and replace the lazy default.");
        Require(filled.GetData().All(value => value == 0x33),
            "A nonzero fill must materialize the packed nibble value.");
        filled.Fill(0);
        Require(filled.IsEmpty(), "Filling with zero must restore the empty lazy representation.");
    }

    private static void CheckCoordinateAndNibbleLayout()
    {
        var layer = new DataLayer();
        layer.Set(0, 0, 0, 0x1F);
        layer.Set(1, 0, 0, 2);
        layer.Set(0, 0, 1, 3);
        layer.Set(0, 1, 0, 4);

        Require(layer.Get(0, 0, 0) == 15, "Set must truncate values to the low four bits.");
        Require(layer.Get(1, 0, 0) == 2, "Odd cell indices must occupy the high nibble.");
        Require(layer.Get(0, 0, 1) == 3, "Z must occupy the middle four index bits.");
        Require(layer.Get(0, 1, 0) == 4, "Y must occupy the high eight index bits.");

        byte[] bytes = layer.GetData();
        Require(bytes[0] == 0x2F, "The first two cells must be packed low-nibble first.");
        Require(bytes[8] == 0x03, "The Z=1 cell must follow the vanilla X/Z/Y index order.");
        Require(bytes[128] == 0x04, "The Y=1 cell must start at index 256.");
    }

    private static void CheckArrayOwnershipAndCopy()
    {
        byte[] supplied = new byte[DataLayer.Size];
        var fromArray = new DataLayer(supplied);
        supplied[0] = 0xA5;
        Require(fromArray.Get(0, 0, 0) == 5 && fromArray.Get(1, 0, 0) == 10,
            "The byte-array constructor must retain the supplied storage.");

        DataLayer lazy = new(6);
        DataLayer lazyCopy = lazy.Copy();
        Require(lazyCopy.IsDefinitelyFilledWith(6),
            "Copying a homogeneous DataLayer must preserve its lazy default representation.");

        DataLayer materializedCopy = fromArray.Copy();
        materializedCopy.GetData()[0] = 0x3C;
        Require(fromArray.Get(0, 0, 0) == 5 && materializedCopy.Get(0, 0, 0) == 12,
            "Copying materialized light data must clone the backing array.");
    }

    private static void CheckFormattingAndEmptyState()
    {
        var layer = new DataLayer(1);
        layer.Set(0, 1, 0, 2);
        Require(layer.ToString().Length == 4096 + 256 + 16,
            "DataLayer debug output must include line and layer separators.");
        Require(layer.LayerToString(0).Length == 256 + 16,
            "Layer debug output must contain 256 hexadecimal values and 16 line separators.");
        Require(layer.LayerToString(0).StartsWith("1", StringComparison.Ordinal) &&
                layer.LayerToString(1).StartsWith("1", StringComparison.Ordinal) &&
                layer.LayerToString(1)[0] == '1' &&
                layer.LayerToString(1)[1] == '1' &&
                layer.LayerToString(1)[2] == '1' &&
                layer.LayerToString(1)[3] == '1' &&
                layer.LayerToString(1)[4] == '1' &&
                layer.LayerToString(1)[5] == '1' &&
                layer.LayerToString(1)[6] == '1' &&
                layer.LayerToString(1)[7] == '1' &&
                layer.LayerToString(1)[8] == '1' &&
                layer.LayerToString(1)[9] == '1' &&
                layer.LayerToString(1)[10] == '1' &&
                layer.LayerToString(1)[11] == '1' &&
                layer.LayerToString(1)[12] == '1' &&
                layer.LayerToString(1)[13] == '1' &&
                layer.LayerToString(1)[14] == '1' &&
                layer.LayerToString(1)[15] == '1',
            "LayerToString must read only the 256-cell slice selected by its layer index.");
        Require(layer.LayerToString(1)[16] == '\\n',
            "LayerToString must retain its 16-row line break format.");
        Require(layer.IsDefinitelyFilledWith(1),
            "Debug formatting must not force materialization of a homogeneous DataLayer.");

        _ = layer.GetData();
        Require(!layer.IsEmpty(), "A materialized zero array is not represented as the lazy empty value.");
    }

    private static void CheckInvalidDataLength()
    {
        try
        {
            _ = new DataLayer(new byte[DataLayer.Size - 1]);
            throw new InvalidOperationException("DataLayer must reject arrays other than 2048 bytes.");
        }
        catch (ArgumentException exception) when (exception.ParamName == "data")
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
