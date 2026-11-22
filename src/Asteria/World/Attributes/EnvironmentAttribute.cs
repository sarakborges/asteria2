using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.Attributes;

public interface IEnvironmentAttribute
{
    bool IsSyncable { get; }
    bool IsPositional { get; }
    bool IsSpatiallyInterpolated { get; }
    bool IsFullResolutionBiomes { get; }
    Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry> CreateEntryCodec();
    Codec<IAttributeTrackDefinition> CreateTrackCodec();
}

/// <summary>A registered, typed environment value definition and its runtime policy flags.</summary>
public sealed class EnvironmentAttribute<T> : IEnvironmentAttribute
{
    public AttributeType<T> Type { get; }
    public T DefaultValue { get; }
    public AttributeRange<T> ValueRange { get; }
    public Codec<T>? ValueCodec { get; }
    public bool IsSyncable { get; }
    public bool IsPositional { get; }
    public bool IsSpatiallyInterpolated { get; }
    public bool IsFullResolutionBiomes { get; }

    private EnvironmentAttribute(
        AttributeType<T> type,
        T defaultValue,
        AttributeRange<T> valueRange,
        bool isSyncable,
        bool isPositional,
        bool isSpatiallyInterpolated,
        bool isFullResolutionBiomes)
    {
        Type = type;
        DefaultValue = defaultValue;
        ValueRange = valueRange;
        ValueCodec = type.ValueCodec?.Validate(value =>
            ValueRange.TryValidate(value, out string? error)
                ? DataResult<T>.Success(value)
                : DataResult<T>.Error(error ?? "Value is outside the allowed range."));
        IsSyncable = isSyncable;
        IsPositional = isPositional;
        IsSpatiallyInterpolated = isSpatiallyInterpolated;
        IsFullResolutionBiomes = isFullResolutionBiomes;
    }

    public static Builder CreateBuilder(AttributeType<T> type) => new(type);

    public T SanitizeValue(T value) => ValueRange.Sanitize(value);

    Codec<EnvironmentAttributeMap.IEnvironmentAttributeEntry> IEnvironmentAttribute.CreateEntryCodec() =>
        new EnvironmentAttributeEntryCodec<T>(this).XMap<EnvironmentAttributeMap.IEnvironmentAttributeEntry>(
            static entry => entry,
            entry => entry is EnvironmentAttributeMap.IEnvironmentAttributeEntry<T> typed
                ? typed
                : throw new InvalidOperationException(
                    $"Entry does not contain a {typeof(T).Name} environment attribute value."));

    Codec<IAttributeTrackDefinition> IEnvironmentAttribute.CreateTrackCodec() =>
        AttributeTrack.CreateCodec(this).XMap<IAttributeTrackDefinition>(
            static track => track,
            track => track is IAttributeTrack<T> typed
                ? typed
                : throw new InvalidOperationException(
                    $"Track does not contain a {typeof(T).Name} environment attribute value."));

    public override string ToString() =>
        BuiltInRegistries.EnvironmentAttribute.GetKey(this)?.ToString() ?? base.ToString()!;

    public sealed class Builder
    {
        private readonly AttributeType<T> _type;
        private T? _defaultValue;
        private bool _hasDefaultValue;
        private AttributeRange<T> _valueRange = AttributeRange<T>.Any();
        private bool _isSyncable;
        private bool _isPositional = true;
        private bool _isSpatiallyInterpolated;
        private bool _isFullResolutionBiomes;

        internal Builder(AttributeType<T> type) =>
            _type = type ?? throw new ArgumentNullException(nameof(type));

        public Builder DefaultValue(T value)
        {
            _defaultValue = value;
            _hasDefaultValue = true;
            return this;
        }

        public Builder ValueRange(AttributeRange<T> valueRange)
        {
            _valueRange = valueRange ?? throw new ArgumentNullException(nameof(valueRange));
            return this;
        }

        public Builder Syncable()
        {
            _isSyncable = true;
            return this;
        }

        public Builder NotPositional()
        {
            _isPositional = false;
            return this;
        }

        public Builder SpatiallyInterpolated()
        {
            _isSpatiallyInterpolated = true;
            return this;
        }

        public Builder FullResolutionBiomes()
        {
            _isFullResolutionBiomes = true;
            return this;
        }

        public EnvironmentAttribute<T> Build()
        {
            if (!_hasDefaultValue || _defaultValue is null)
                throw new InvalidOperationException("Missing default value");

            return new EnvironmentAttribute<T>(
                _type,
                _defaultValue,
                _valueRange,
                _isSyncable,
                _isPositional,
                _isSpatiallyInterpolated,
                _isFullResolutionBiomes);
        }
    }
}
