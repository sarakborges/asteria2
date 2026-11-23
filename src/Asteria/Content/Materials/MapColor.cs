namespace Asteria.Content.Materials;

/// <summary>
/// A registered palette entry used to encode block colors in maps and related visual data.
/// The color table and packed brightness representation follow Minecraft Java 26.3.
/// </summary>
public sealed class MapColor
{
    private static readonly MapColor?[] MATERIAL_COLORS = new MapColor[64];

    public static readonly MapColor NONE = new(0, 0);
    public static readonly MapColor GRASS = new(1, 8368696);
    public static readonly MapColor SAND = new(2, 16247203);
    public static readonly MapColor WOOL = new(3, 13092807);
    public static readonly MapColor FIRE = new(4, 16711680);
    public static readonly MapColor ICE = new(5, 10526975);
    public static readonly MapColor METAL = new(6, 10987431);
    public static readonly MapColor PLANT = new(7, 31744);
    public static readonly MapColor SNOW = new(8, 16777215);
    public static readonly MapColor CLAY = new(9, 10791096);
    public static readonly MapColor DIRT = new(10, 9923917);
    public static readonly MapColor STONE = new(11, 7368816);
    public static readonly MapColor WATER = new(12, 4210943);
    public static readonly MapColor WOOD = new(13, 9402184);
    public static readonly MapColor QUARTZ = new(14, 16776437);
    public static readonly MapColor COLOR_ORANGE = new(15, 14188339);
    public static readonly MapColor COLOR_MAGENTA = new(16, 11685080);
    public static readonly MapColor COLOR_LIGHT_BLUE = new(17, 6724056);
    public static readonly MapColor COLOR_YELLOW = new(18, 15066419);
    public static readonly MapColor COLOR_LIGHT_GREEN = new(19, 8375321);
    public static readonly MapColor COLOR_PINK = new(20, 15892389);
    public static readonly MapColor COLOR_GRAY = new(21, 5000268);
    public static readonly MapColor COLOR_LIGHT_GRAY = new(22, 10066329);
    public static readonly MapColor COLOR_CYAN = new(23, 5013401);
    public static readonly MapColor COLOR_PURPLE = new(24, 8339378);
    public static readonly MapColor COLOR_BLUE = new(25, 3361970);
    public static readonly MapColor COLOR_BROWN = new(26, 6704179);
    public static readonly MapColor COLOR_GREEN = new(27, 6717235);
    public static readonly MapColor COLOR_RED = new(28, 10040115);
    public static readonly MapColor COLOR_BLACK = new(29, 1644825);
    public static readonly MapColor GOLD = new(30, 16445005);
    public static readonly MapColor DIAMOND = new(31, 6085589);
    public static readonly MapColor LAPIS = new(32, 4882687);
    public static readonly MapColor EMERALD = new(33, 55610);
    public static readonly MapColor PODZOL = new(34, 8476209);
    public static readonly MapColor NETHER = new(35, 7340544);
    public static readonly MapColor TERRACOTTA_WHITE = new(36, 13742497);
    public static readonly MapColor TERRACOTTA_ORANGE = new(37, 10441252);
    public static readonly MapColor TERRACOTTA_MAGENTA = new(38, 9787244);
    public static readonly MapColor TERRACOTTA_LIGHT_BLUE = new(39, 7367818);
    public static readonly MapColor TERRACOTTA_YELLOW = new(40, 12223780);
    public static readonly MapColor TERRACOTTA_LIGHT_GREEN = new(41, 6780213);
    public static readonly MapColor TERRACOTTA_PINK = new(42, 10505550);
    public static readonly MapColor TERRACOTTA_GRAY = new(43, 3746083);
    public static readonly MapColor TERRACOTTA_LIGHT_GRAY = new(44, 8874850);
    public static readonly MapColor TERRACOTTA_CYAN = new(45, 5725276);
    public static readonly MapColor TERRACOTTA_PURPLE = new(46, 8014168);
    public static readonly MapColor TERRACOTTA_BLUE = new(47, 4996700);
    public static readonly MapColor TERRACOTTA_BROWN = new(48, 4993571);
    public static readonly MapColor TERRACOTTA_GREEN = new(49, 5001770);
    public static readonly MapColor TERRACOTTA_RED = new(50, 9321518);
    public static readonly MapColor TERRACOTTA_BLACK = new(51, 2430480);
    public static readonly MapColor CRIMSON_NYLIUM = new(52, 12398641);
    public static readonly MapColor CRIMSON_STEM = new(53, 9715553);
    public static readonly MapColor CRIMSON_HYPHAE = new(54, 6035741);
    public static readonly MapColor WARPED_NYLIUM = new(55, 1474182);
    public static readonly MapColor WARPED_STEM = new(56, 3837580);
    public static readonly MapColor WARPED_HYPHAE = new(57, 5647422);
    public static readonly MapColor WARPED_WART_BLOCK = new(58, 1356933);
    public static readonly MapColor DEEPSLATE = new(59, 6579300);
    public static readonly MapColor RAW_IRON = new(60, 14200723);
    public static readonly MapColor GLOW_LICHEN = new(61, 8365974);

    public int Id { get; }
    public int Col { get; }

    private MapColor(int id, int col)
    {
        if ((uint)id > 63U)
            throw new IndexOutOfRangeException("Map colour ID must be between 0 and 63 (inclusive)");

        Id = id;
        Col = col;
        MATERIAL_COLORS[id] = this;
    }

    public int CalculateArgbColor(Brightness brightness)
    {
        ArgumentNullException.ThrowIfNull(brightness);
        if (ReferenceEquals(this, NONE))
            return 0;

        int opaque = unchecked((int)0xFF000000) | Col;
        return ScaleRgb(opaque, brightness.Modifier);
    }

    public static MapColor ById(int id)
    {
        // Preconditions.checkPositionIndex permits id == length; the following array access
        // then throws, which is observable in the 26.3 implementation.
        if (id < 0 || id > MATERIAL_COLORS.Length)
            throw new ArgumentOutOfRangeException(nameof(id), id, "material id");

        return ByIdUnsafe(id);
    }

    private static MapColor ByIdUnsafe(int id)
    {
        MapColor? result = MATERIAL_COLORS[id];
        return result ?? NONE;
    }

    public static int GetColorFromPackedId(int packedId)
    {
        int value = packedId & 0xFF;
        return ByIdUnsafe(value >> 2).CalculateArgbColor(Brightness.ByIdUnsafe(value & 3));
    }

    public byte GetPackedId(Brightness brightness)
    {
        ArgumentNullException.ThrowIfNull(brightness);
        return unchecked((byte)(Id << 2 | brightness.Id & 3));
    }

    private static int ScaleRgb(int color, int scale)
    {
        static int ScaleChannel(int channel, int amount) =>
            (int)Math.Clamp((long)channel * amount / 255L, 0L, 255L);

        int alpha = (int)((uint)color >> 24);
        int red = color >> 16 & 0xFF;
        int green = color >> 8 & 0xFF;
        int blue = color & 0xFF;

        return alpha << 24 |
            ScaleChannel(red, scale) << 16 |
            ScaleChannel(green, scale) << 8 |
            ScaleChannel(blue, scale);
    }

    public sealed class Brightness
    {
        public static readonly Brightness LOW = new(0, 180);
        public static readonly Brightness NORMAL = new(1, 220);
        public static readonly Brightness HIGH = new(2, 255);
        public static readonly Brightness LOWEST = new(3, 135);

        private static readonly Brightness[] VALUES =
        {
            LOW, NORMAL, HIGH, LOWEST
        };

        public int Id { get; }
        public int Modifier { get; }

        private Brightness(int id, int modifier)
        {
            Id = id;
            Modifier = modifier;
        }

        public static Brightness ById(int id)
        {
            if (id < 0 || id > VALUES.Length)
                throw new ArgumentOutOfRangeException(nameof(id), id, "brightness id");

            return ByIdUnsafe(id);
        }

        private static Brightness ByIdUnsafe(int id) =>
            VALUES[id];
    }
}
