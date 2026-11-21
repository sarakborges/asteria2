using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>Options contract for a registered particle type.</summary>
public interface IParticleOptions
{
    ParticleType GetParticleType();
}

/// <summary>
/// Registry value describing a particle options type and its local data codec.
/// Packet stream codecs are intentionally omitted for the offline-only game.
/// </summary>
public abstract class ParticleType
{
    public bool OverrideLimiter { get; }

    protected ParticleType(bool overrideLimiter) =>
        OverrideLimiter = overrideLimiter;

    internal abstract MapCodec<IParticleOptions> OptionsCodec { get; }
}

/// <summary>A particle type with a statically known options representation.</summary>
public abstract class ParticleType<TOptions> : ParticleType
    where TOptions : class, IParticleOptions
{
    protected ParticleType(bool overrideLimiter) : base(overrideLimiter) { }

    public abstract MapCodec<TOptions> Codec { get; }

    internal sealed override MapCodec<IParticleOptions> OptionsCodec =>
        Codec.XMap<IParticleOptions>(
            static options => options,
            options => options is TOptions typed
                ? typed
                : throw new InvalidOperationException(
                    $"Particle options {options.GetType().Name} do not belong to {typeof(TOptions).Name}."));
}

/// <summary>Payload-free particle options resolving to the registered singleton instance.</summary>
public sealed class SimpleParticleType : ParticleType<SimpleParticleType>, IParticleOptions
{
    private readonly MapCodec<SimpleParticleType> _codec;

    public override MapCodec<SimpleParticleType> Codec => _codec;

    internal SimpleParticleType(bool overrideLimiter) : base(overrideLimiter) =>
        _codec = MapCodec<SimpleParticleType>.Unit(this);

    public ParticleType GetParticleType() => this;
}
