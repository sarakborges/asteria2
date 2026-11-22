using Asteria.Serialization;

namespace Asteria.World.GameEvents;

/// <summary>Registered discriminator and payload codec for a position-source variant.</summary>
public abstract class PositionSourceType
{
    internal abstract MapCodec<IPositionSource> DispatchCodec { get; }
}

/// <summary>A position-source type with a statically known payload model.</summary>
public abstract class PositionSourceType<TSource> : PositionSourceType
    where TSource : class, IPositionSource
{
    public abstract MapCodec<TSource> Codec { get; }

    internal sealed override MapCodec<IPositionSource> DispatchCodec =>
        Codec.XMap<IPositionSource>(
            static source => source,
            source => source is TSource typed
                ? typed
                : throw new InvalidOperationException(
                    $"Position source {source.GetType().Name} does not belong to {typeof(TSource).Name}."));
}
