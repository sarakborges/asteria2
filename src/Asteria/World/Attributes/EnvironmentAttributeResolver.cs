namespace Asteria.World.Attributes;

/// <summary>Resolves one attribute by applying source maps from lowest to highest priority.</summary>
public static class EnvironmentAttributeResolver
{
    /// <summary>
    /// Resolves an attribute from its registered default, applying each source in low-to-high priority order.
    /// The intended source order is dimension, biome, timeline, then weather.
    /// </summary>
    public static T Resolve<T>(
        EnvironmentAttribute<T> attribute,
        params EnvironmentAttributeMap[] sources)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return Resolve(attribute, attribute.DefaultValue, sources);
    }

    /// <summary>
    /// Resolves an attribute from an explicit lower-layer value, applying each source in low-to-high priority order.
    /// </summary>
    public static T Resolve<T>(
        EnvironmentAttribute<T> attribute,
        T baseValue,
        params EnvironmentAttributeMap[] sources)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(sources);

        T value = baseValue;
        foreach (EnvironmentAttributeMap source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            value = source.ApplyModifier(attribute, value);
        }

        return value;
    }
}
