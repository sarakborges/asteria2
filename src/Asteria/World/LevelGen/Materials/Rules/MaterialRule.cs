using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;

namespace Asteria.World.LevelGen.Materials.Rules;

/// <summary>
/// Serializable material-selection rule graph from Minecraft Java 26.3.
/// </summary>
public interface MaterialRule
{
    static Codec<MaterialRule> DIRECT_CODEC { get; } = CreateDirectCodec();

    static Codec<Holder<MaterialRule>> HOLDER_CODEC { get; } =
        RegistryCodecs.Holder(Registries.MATERIAL_RULE, DIRECT_CODEC);

    static Codec<MaterialRule> CODEC { get; } =
        HOLDER_CODEC.XMap<MaterialRule>(
            static holder => holder.Kind == HolderKind.Direct
                ? holder.Value
                : new HolderHolder(holder),
            static rule => rule is HolderHolder holder
                ? holder.Rule
                : Holder<MaterialRule>.Direct(rule));

    RuleEvaluator Compile(MaterialRuleContext context);

    MapCodec<MaterialRule> Codec();

    private static Codec<MaterialRule> CreateDirectCodec()
    {
        MapCodec<ResourceLocation> typeField = Codecs.STRING.FlatXMap(
            static name =>
            {
                ResourceLocation? identifier = ResourceLocation.TryParse(name);
                return identifier.HasValue
                    ? DataResult<ResourceLocation>.Success(identifier.Value)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Invalid registry key '{name}' for {BuiltInRegistries.MaterialRuleType.Key}");
            },
            static identifier => DataResult<string>.Success(identifier.ToString()))
            .FieldOf("type");

        MapCodec<MaterialRule> dispatch = MapCodec<MaterialRule>.Dispatch<ResourceLocation>(
            typeField,
            static rule =>
            {
                MapCodec<MaterialRule> codec = rule.Codec();
                RegistryKey<MapCodec<MaterialRule>>? key =
                    BuiltInRegistries.MaterialRuleType.GetResourceKey(codec);
                return key.HasValue
                    ? DataResult<ResourceLocation>.Success(key.Value.Location)
                    : DataResult<ResourceLocation>.Error(
                        () => $"Unregistered material rule codec '{codec}'");
            },
            static identifier =>
            {
                MapCodec<MaterialRule>? codec =
                    BuiltInRegistries.MaterialRuleType.GetValue(identifier);
                return codec is not null
                    ? DataResult<MapCodec<MaterialRule>>.Success(codec)
                    : DataResult<MapCodec<MaterialRule>>.Error(
                        () => $"Unknown registry key '{identifier}' in {BuiltInRegistries.MaterialRuleType.Key}");
            });

        return dispatch.Codec();
    }

    public sealed record HolderHolder(Holder<MaterialRule> Rule) : MaterialRule
    {
        public RuleEvaluator Compile(MaterialRuleContext context) =>
            Rule.Value.Compile(context);

        public MapCodec<MaterialRule> Codec() =>
            throw new NotSupportedException("HolderHolder cannot be serialized");
    }
}
