using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Serializable material-condition graph from Minecraft Java 26.3.</summary>
public interface MaterialCondition
{
    static Codec<MaterialCondition> DIRECT_CODEC { get; } = CreateDirectCodec();

    static Codec<Holder<MaterialCondition>> HOLDER_CODEC { get; } =
        RegistryCodecs.Holder(Registries.MATERIAL_CONDITION, DIRECT_CODEC);

    static Codec<MaterialCondition> CODEC { get; } =
        HOLDER_CODEC.XMap<MaterialCondition>(
            static holder => holder.Kind == HolderKind.Direct ? holder.Value : new HolderHolder(holder),
            static condition => condition is HolderHolder holder
                ? holder.Condition
                : Holder<MaterialCondition>.Direct(condition));

    ConditionEvaluator Compile(MaterialRuleContext context);
    MapCodec<MaterialCondition> Codec();

    private static Codec<MaterialCondition> CreateDirectCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            static name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.MaterialConditionType.Key}");
            },
            static identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<MaterialCondition> dispatch = MapCodec<MaterialCondition>.Dispatch<ResourceLocation>(
            typeField,
            static condition =>
            {
                MapCodec<MaterialCondition> codec = condition.Codec();
                RegistryKey<MapCodec<MaterialCondition>>? key =
                    BuiltInRegistries.MaterialConditionType.GetResourceKey(codec);
                return key.HasValue
                    ? DataResult<ResourceLocation>.Success(key.Value.Location)
                    : DataResult<ResourceLocation>.Error(() => $"Unregistered material condition codec '{codec}'");
            },
            static identifier =>
            {
                MapCodec<MaterialCondition>? codec =
                    BuiltInRegistries.MaterialConditionType.GetValue(identifier);
                return codec is not null
                    ? DataResult<MapCodec<MaterialCondition>>.Success(codec)
                    : DataResult<MapCodec<MaterialCondition>>.Error(
                        () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.MaterialConditionType.Key}");
            });
        return dispatch.Codec();
    }

    public sealed record HolderHolder(Holder<MaterialCondition> Condition) : MaterialCondition
    {
        public ConditionEvaluator Compile(MaterialRuleContext context) =>
            Condition.Value.Compile(context);
        public MapCodec<MaterialCondition> Codec() =>
            throw new NotSupportedException("HolderHolder cannot be serialized");
    }
}
