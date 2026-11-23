using Asteria.Content.Identifiers;

namespace Asteria.Util.Context;

/// <summary>
/// Typed context parameter key from Minecraft Java 26.3. Equality is intentionally reference-based.
/// </summary>
public sealed class ContextKey<T>
{
    private readonly ResourceLocation _name;

    public ContextKey(ResourceLocation name) => _name = name;

    public static ContextKey<T> Vanilla(string name) =>
        new(ResourceLocation.WithDefaultNamespace(name));

    public ResourceLocation Name() => _name;

    public override string ToString() => $"<parameter {_name}>";
}
