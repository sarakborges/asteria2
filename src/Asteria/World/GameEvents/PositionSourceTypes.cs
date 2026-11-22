using Asteria.Content.Identifiers;
using Asteria.Content.Registries;

namespace Asteria.World.GameEvents;

/// <summary>Built-in position-source variants supported by the local game.</summary>
public static class PositionSourceTypes
{
    public static readonly PositionSourceType<BlockPositionSource> BLOCK =
        Register("block", new BlockPositionSource.Type());

    internal static void RegisterAll()
    {
        _ = BLOCK;
    }

    private static PositionSourceType<TSource> Register<TSource>(
        string name,
        PositionSourceType<TSource> type)
        where TSource : class, IPositionSource
    {
        ResourceLocation location = ResourceLocation.WithDefaultNamespace(name);
        RegistryKey<PositionSourceType> key =
            RegistryKey<PositionSourceType>.Create(BuiltInRegistries.PositionSourceType.Key, location);
        BuiltInRegistries.PositionSourceType.Register(key, type, RegistrationInfo.BuiltIn);
        return type;
    }
}
