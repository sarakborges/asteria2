using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;

namespace Asteria.World.Clocks;

/// <summary>An empty registered clock definition. Runtime state belongs to ClockInstance.</summary>
public sealed record WorldClock()
{
    public static Codec<WorldClock> DIRECT_CODEC { get; } =
        MapCodec<WorldClock>.Unit(static () => new WorldClock()).Codec();

    public static Codec<Holder<WorldClock>> CODEC { get; } =
        Codecs.STRING.FlatXMap(
            name =>
            {
                ResourceLocation? location = ResourceLocation.TryParse(name);
                if (!location.HasValue)
                {
                    return DataResult<Holder<WorldClock>>.Error(() =>
                        $"Invalid registry key '{name}' for {BuiltInRegistries.WorldClock.Key}");
                }

                Holder<WorldClock>.Reference? holder =
                    BuiltInRegistries.WorldClock.GetHolder(location.Value);
                return holder is not null
                    ? DataResult<Holder<WorldClock>>.Success(
                        holder, BuiltInRegistries.WorldClock.RegistryLifecycle)
                    : DataResult<Holder<WorldClock>>.Error(() =>
                        $"Unknown registry key '{name}' in {BuiltInRegistries.WorldClock.Key}");
            },
            holder =>
            {
                ArgumentNullException.ThrowIfNull(holder);
                RegistryKey<WorldClock>? key = holder.KeyOrNull;
                return key.HasValue && key.Value.IsFor(BuiltInRegistries.WorldClock.Key)
                    ? DataResult<string>.Success(
                        key.Value.Location.ToString(), BuiltInRegistries.WorldClock.RegistryLifecycle)
                    : DataResult<string>.Error(() =>
                        $"Unregistered world clock holder '{holder}'");
            });
}
