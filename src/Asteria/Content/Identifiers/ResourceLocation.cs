namespace Asteria.Content.Identifiers;

/// <summary>
/// A stable, namespaced identifier for registry entries.
/// Examples: "asteria:registry_test", "minecraft:stone".
/// </summary>
public readonly record struct ResourceLocation
{
    public string Namespace { get; }
    public string Path { get; }

    public bool IsValid =>
        Namespace is not null &&
        Path is not null &&
        IsValidNamespace(Namespace) &&
        IsValidPath(Path);

    public ResourceLocation(string @namespace, string path)
    {
        ArgumentNullException.ThrowIfNull(@namespace);
        ArgumentNullException.ThrowIfNull(path);

        if (!IsValidNamespace(@namespace))
        {
            throw new ArgumentException($"Invalid resource namespace '{@namespace}'.", nameof(@namespace));
        }

        if (!IsValidPath(path))
        {
            throw new ArgumentException($"Invalid resource path '{path}'.", nameof(path));
        }

        Namespace = @namespace;
        Path = path;
    }

    public static ResourceLocation Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int separator = value.IndexOf(':');
        if (separator < 0)
        {
            return new ResourceLocation("minecraft", value);
        }

        string path = value[(separator + 1)..];
        string @namespace = separator == 0 ? "minecraft" : value[..separator];
        return new ResourceLocation(@namespace, path);
    }

    public static bool TryParse(string? value, out ResourceLocation location)
    {
        location = default;

        if (value is null)
        {
            return false;
        }

        try
        {
            location = Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static ResourceLocation FromNamespaceAndPath(string @namespace, string path) =>
        new(@namespace, path);

    public override string ToString() => $"{Namespace}:{Path}";

    private static bool IsValidNamespace(string value)
    {
        if (value == "..")
        {
            return false;
        }

        foreach (char character in value)
        {
            bool valid =
                character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9' ||
                character is '_' or '-' or '.';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPath(string value)
    {
        foreach (char character in value)
        {
            bool valid =
                character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9' ||
                character is '_' or '-' or '.' or '/';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
