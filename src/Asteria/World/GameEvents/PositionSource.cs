using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;

namespace Asteria.World.GameEvents;

/// <summary>
/// A local world-position source. A nullable position preserves the optional result
/// semantics; world-dependent source variants can be added when their domains exist.
/// </summary>
public interface IPositionSource
{
    Vec3? GetPosition();
    PositionSourceType GetSourceType();
}

/// <summary>Registry dispatch codec for local position sources.</summary>
public static class PositionSourceCodecs
{
    public static Codec<IPositionSource> CODEC { get; } =
        MapCodec<IPositionSource>.Dispatch(
            BuiltInRegistries.PositionSourceType.ByNameCodec().FieldOf("type"),
            static source => DataResult<PositionSourceType>.Success(source.GetSourceType()),
            static type => DataResult<MapCodec<IPositionSource>>.Success(type.DispatchCodec))
        .Codec();
}
