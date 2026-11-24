using Asteria.Serialization;

namespace Asteria.World.LevelGen.Structure.Pools;

/// <summary>
/// Jigsaw connection metadata from Minecraft Java 26.3.
/// Serialization intentionally uses DynamicOps field defaults, not a required-field record codec.
/// </summary>
public sealed class JigsawJunction : IEquatable<JigsawJunction>
{
    private readonly int _sourceX;
    private readonly int _sourceGroundY;
    private readonly int _sourceZ;
    private readonly int _deltaY;
    private readonly StructureTemplatePool.Projection? _destProjection;

    public JigsawJunction(
        int sourceX,
        int sourceGroundY,
        int sourceZ,
        int deltaY,
        StructureTemplatePool.Projection? destProjection)
    {
        _sourceX = sourceX;
        _sourceGroundY = sourceGroundY;
        _sourceZ = sourceZ;
        _deltaY = deltaY;
        _destProjection = destProjection;
    }

    public int GetSourceX() => _sourceX;

    public int GetSourceGroundY() => _sourceGroundY;

    public int GetSourceZ() => _sourceZ;

    public int GetDeltaY() => _deltaY;

    public StructureTemplatePool.Projection? GetDestProjection() => _destProjection;

    /// <summary>
    /// Writes the exact five-field Dynamic map used by JigsawJunction.serialize.
    /// </summary>
    public T Serialize<T>(DynamicOps<T> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);

        return ops.CreateMap(new[]
        {
            new KeyValuePair<T, T>(
                ops.CreateString("source_x"),
                ops.CreateInt(_sourceX)),
            new KeyValuePair<T, T>(
                ops.CreateString("source_ground_y"),
                ops.CreateInt(_sourceGroundY)),
            new KeyValuePair<T, T>(
                ops.CreateString("source_z"),
                ops.CreateInt(_sourceZ)),
            new KeyValuePair<T, T>(
                ops.CreateString("delta_y"),
                ops.CreateInt(_deltaY)),
            new KeyValuePair<T, T>(
                ops.CreateString("dest_proj"),
                ops.CreateString(_destProjection!.GetName()))
        });
    }

    /// <summary>
    /// Reads integer fields with zero defaults and resolves dest_proj with Projection.byName,
    /// matching Dynamic.asInt(0), Dynamic.asString("") and StructureTemplatePool.Projection.byName.
    /// </summary>
    public static JigsawJunction Deserialize<T>(DynamicOps<T> ops, T input)
    {
        ArgumentNullException.ThrowIfNull(ops);

        int sourceX = ReadInt(ops, input, "source_x");
        int sourceGroundY = ReadInt(ops, input, "source_ground_y");
        int sourceZ = ReadInt(ops, input, "source_z");
        int deltaY = ReadInt(ops, input, "delta_y");
        string projectionName = ReadString(ops, input, "dest_proj");

        return new JigsawJunction(
            sourceX,
            sourceGroundY,
            sourceZ,
            deltaY,
            StructureTemplatePool.Projection.ByName(projectionName));
    }

    private static int ReadInt<T>(DynamicOps<T> ops, T input, string field)
    {
        if (!ops.Get(input, field).TryGetResult(out T value))
            return 0;

        return ops.GetNumberValue(value).TryGetResult(out NumberValue number)
            ? number.IntValue
            : 0;
    }

    private static string ReadString<T>(DynamicOps<T> ops, T input, string field)
    {
        if (!ops.Get(input, field).TryGetResult(out T value))
            return string.Empty;

        return ops.GetStringValue(value).TryGetResult(out string result)
            ? result
            : string.Empty;
    }

    public bool Equals(JigsawJunction? other) =>
        ReferenceEquals(this, other) ||
        (other is not null &&
         _sourceX == other._sourceX &&
         _sourceZ == other._sourceZ &&
         _deltaY == other._deltaY &&
         ReferenceEquals(_destProjection, other._destProjection));

    public override bool Equals(object? obj) =>
        obj is JigsawJunction other && Equals(other);

    public override int GetHashCode()
    {
        int result = _sourceX;
        result = unchecked(31 * result + _sourceGroundY);
        result = unchecked(31 * result + _sourceZ);
        result = unchecked(31 * result + _deltaY);
        return unchecked(31 * result + _destProjection!.GetHashCode());
    }

    public override string ToString() =>
        $"JigsawJunction{{sourceX={_sourceX}, sourceGroundY={_sourceGroundY}, sourceZ={_sourceZ}, deltaY={_deltaY}, destProjection={_destProjection?.ToString() ?? "null"}}}";
}
