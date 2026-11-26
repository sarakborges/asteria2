using Asteria.Content.Identifiers;
using Asteria.Serialization;

internal static class ResourceLocationChecks
{
    public static void Run()
    {
        CheckDefaultNamespace();
        CheckParsingAndValidation();
        CheckCodec();
        CheckShortNamesAndPaths();
    }

    private static void CheckDefaultNamespace()
    {
        Require(
            ResourceLocation.DefaultNamespace == "minecraft",
            "The default identifier namespace must match Minecraft Java 26.3.");
        Require(
            ResourceLocation.Parse("block/stone") == new ResourceLocation("minecraft", "block/stone"),
            "Unqualified vanilla asset identifiers must resolve in the minecraft namespace.");
        Require(
            ResourceLocation.Parse(":block/stone") == new ResourceLocation("minecraft", "block/stone"),
            "An identifier with an empty namespace must use minecraft.");
        Require(
            ResourceLocation.WithDefaultNamespace("block/stone").Namespace == "minecraft",
            "WithDefaultNamespace must use the Minecraft namespace.");
        Require(
            ResourceLocation.Parse("asteria:custom_block").Namespace == "asteria",
            "Project-owned assets remain supported through an explicit namespace.");
    }

    private static void CheckParsingAndValidation()
    {
        Require(
            ResourceLocation.TryParse("minecraft:block/stone") is { } explicitId &&
            explicitId.Path == "block/stone",
            "TryParse must preserve a valid explicit identifier.");
        Require(
            ResourceLocation.TryParse("Bad Namespace:block/stone") is null,
            "TryParse must reject invalid namespace characters.");
        Require(
            ResourceLocation.TryParse("minecraft:block/stone?variant") is null,
            "TryParse must reject invalid path characters.");
        Require(
            ResourceLocation.TryParse("..:block/stone") is null,
            "TryParse must reject the parent-directory namespace.");
        Require(
            ResourceLocation.Parse("minecraft:block/stone").ToString() == "minecraft:block/stone",
            "Identifier string formatting must preserve namespace and path.");
    }

    private static void CheckCodec()
    {
        DynamicOps<JsonElement> ops = JsonOps.INSTANCE;
        ResourceLocation source = ResourceLocation.Parse("block/stone");
        JsonElement encoded = ResourceLocation.CODEC.EncodeStart(ops, source).GetOrThrow();

        Require(
            ops.GetStringValue(encoded).GetOrThrow() == "minecraft:block/stone",
            "ResourceLocation codec must encode the canonical namespaced string.");

        ResourceLocation decoded = ResourceLocation.CODEC.Parse(
            ops,
            ops.CreateString("block/stone")).GetOrThrow();

        Require(
            decoded == source,
            "ResourceLocation codec must parse vanilla shorthand identifiers with the minecraft namespace.");

        Require(
            !ResourceLocation.CODEC.Parse(ops, ops.CreateString("Invalid Namespace:block")).IsSuccess,
            "ResourceLocation codec must return an error for malformed identifiers.");

        Require(
            ResourceLocation.CODEC.Parse(ops, ops.CreateString("block/stone")).Lifecycle.ToString() == "Stable",
            "ResourceLocation codec must use the stable lifecycle.");
    }

    private static void CheckShortNamesAndPaths()
    {
        ResourceLocation vanilla = ResourceLocation.Parse("minecraft:block/stone");
        ResourceLocation custom = ResourceLocation.Parse("asteria:block/custom");

        Require(
            vanilla.ToShortString() == "block/stone" &&
            custom.ToShortString() == "asteria:block/custom",
            "Short identifier formatting must omit only the default minecraft namespace.");
        Require(
            vanilla.ToShortLanguageKey() == "block/stone" &&
            custom.ToShortLanguageKey() == "asteria.block/custom",
            "Short language keys must omit only the default minecraft namespace.");
        Require(
            ResourceLocation.Parse("minecraft:block/stone").WithSuffix("_side").ToString() ==
            "minecraft:block/stone_side",
            "Identifier path suffix operations must preserve the namespace.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
