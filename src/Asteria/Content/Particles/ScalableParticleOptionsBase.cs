using System.Globalization;
using Asteria.Serialization;

namespace Asteria.Content.Particles;

/// <summary>
/// Shared scale contract for particle options whose decoded scale is validated and
/// whose constructor clamps values to the supported range.
/// </summary>
public abstract class ScalableParticleOptionsBase : IParticleOptions
{
    public const float MIN_SCALE = 0.01F;
    public const float MAX_SCALE = 4.0F;

    protected static readonly Codec<float> SCALE = Codecs.FLOAT.Validate(value =>
        value >= MIN_SCALE && value <= MAX_SCALE
            ? DataResult<float>.Success(value)
            : DataResult<float>.Error(
                () => "Value must be within range [0.01;4.0]: " +
                      value.ToString("R", CultureInfo.InvariantCulture)));

    private readonly float _scale;

    protected ScalableParticleOptionsBase(float scale) =>
        _scale = Math.Clamp(scale, MIN_SCALE, MAX_SCALE);

    public float GetScale() => _scale;

    public abstract ParticleType GetParticleType();
}
