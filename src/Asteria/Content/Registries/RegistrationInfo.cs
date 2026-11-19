namespace Asteria.Content.Registries;

/// <summary>
/// Metadata associated with a registry entry. Known-pack metadata is added with
/// the resource-pack system; lifecycle is part of the current mapped-registry contract.
/// </summary>
public sealed record RegistrationInfo(Lifecycle Lifecycle)
{
    public static RegistrationInfo BuiltIn { get; } = new(Lifecycle.Stable());
}
