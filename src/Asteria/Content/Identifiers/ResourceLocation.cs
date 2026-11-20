namespace Asteria.Content.Identifiers;

/// <summary>
/// A stable, namespaced identifier for registry entries.
/// Examples: "asteria:registry_test", "minecraft:stone".
/// </summary>
public readonly record struct ResourceLocation : IComparable<ResourceLocation>
{
    public const char NamespaceSeparator = ':';
    public const string DefaultNamespace = "minecraft";
    public const string RealmsNamespace = "realms";
    public const string AllowedNamespaceCharacters = "[a-z0-9_.-]";

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

    public static ResourceLocation Parse(string value) =>
        BySeparator(value, NamespaceSeparator);

    public static ResourceLocation WithDefaultNamespace(string path) =>
        new(DefaultNamespace, path);

    public static ResourceLocation BySeparator(string value, char separator)
    {
        ArgumentNullException.ThrowIfNull(value);

        int separatorIndex = value.IndexOf(separator);
        if (separatorIndex >= 0)
        {
            string path = value[(separatorIndex + 1)..];
            if (separatorIndex != 0)
            {
                string @namespace = value[..separatorIndex];
                return new ResourceLocation(@namespace, path);
            }

            return WithDefaultNamespace(path);
        }

        return WithDefaultNamespace(value);
    }

    public static ResourceLocation? TryParse(string? value) =>
        value is null ? null : TryBySeparator(value, NamespaceSeparator);

    public static ResourceLocation? TryBySeparator(string value, char separator)
    {
        ArgumentNullException.ThrowIfNull(value);

        int separatorIndex = value.IndexOf(separator);
        if (separatorIndex >= 0)
        {
            string path = value[(separatorIndex + 1)..];
            if (!IsValidPath(path))
            {
                return null;
            }

            if (separatorIndex != 0)
            {
                string @namespace = value[..separatorIndex];
                return IsValidNamespace(@namespace)
                    ? new ResourceLocation(@namespace, path)
                    : null;
            }

            return new ResourceLocation(DefaultNamespace, path);
        }

        return IsValidPath(value)
            ? new ResourceLocation(DefaultNamespace, value)
            : null;
    }

    public static bool TryParse(string? value, out ResourceLocation location)
    {
        ResourceLocation? parsed = TryParse(value);
        location = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    public static ResourceLocation FromNamespaceAndPath(string @namespace, string path) =>
        new(@namespace, path);

    public ResourceLocation WithPath(string path) =>
        new(Namespace, path);

    public ResourceLocation WithPath(Func<string, string> modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        return WithPath(modifier(Path));
    }

    public ResourceLocation WithPrefix(string prefix) =>
        WithPath(prefix + Path);

    public ResourceLocation WithSuffix(string suffix) =>
        WithPath(Path + suffix);

    public string ResolveAgainst(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        string resultingPath = System.IO.Path.Combine(root, Namespace, Path);
        string normalizedPath = System.IO.Path.GetFullPath(resultingPath);
        string normalizedRoot = System.IO.Path.GetFullPath(root);
        string relativePath = System.IO.Path.GetRelativePath(normalizedRoot, normalizedPath);
        string parentPrefix = $"..{System.IO.Path.DirectorySeparatorChar}";

        if (relativePath == ".." ||
            relativePath.StartsWith(parentPrefix, StringComparison.Ordinal) ||
            System.IO.Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                $"Identifier \"{this}\" tried to access path \"{normalizedPath}\" from root \"{normalizedRoot}\"");
        }

        return resultingPath;
    }

    public string ToDebugFileName() =>
        ToString().Replace('/', '_').Replace(':', '_');

    public string ToLanguageKey() =>
        $"{Namespace}.{Path}";

    public string ToShortLanguageKey() =>
        Namespace == "minecraft" ? Path : ToLanguageKey();

    public string ToShortString() =>
        Namespace == DefaultNamespace ? Path : ToString();

    public string ToLanguageKey(string prefix) =>
        $"{prefix}.{ToLanguageKey()}";

    public string ToLanguageKey(string prefix, string suffix) =>
        $"{prefix}.{ToLanguageKey()}.{suffix}";

    public int CompareTo(ResourceLocation other)
    {
        int result = string.Compare(Path, other.Path, StringComparison.Ordinal);
        return result != 0
            ? result
            : string.Compare(Namespace, other.Namespace, StringComparison.Ordinal);
    }

    public override string ToString() => $"{Namespace}:{Path}";

    public static ResourceLocation? TryBuild(string? @namespace, string? path)
    {
        if (@namespace is null || path is null ||
            !IsValidNamespace(@namespace) || !IsValidPath(path))
        {
            return null;
        }

        return new ResourceLocation(@namespace, path);
    }

    public static bool IsAllowedInIdentifier(char character) =>
        character is >= '0' and <= '9' ||
        character is >= 'a' and <= 'z' ||
        character is '_' or ':' or '/' or '.' or '-';

    public static bool IsValidPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        foreach (char character in value)
        {
            if (!ValidPathChar(character))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidNamespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value == "..")
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!ValidNamespaceChar(character))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ValidPathChar(char character) =>
        character is '_' or '-' or '/' ||
        character is >= 'a' and <= 'z' ||
        character is >= '0' and <= '9' ||
        character == '.';

    private static bool ValidNamespaceChar(char character) =>
        character is '_' or '-' or '.' ||
        character is >= 'a' and <= 'z' ||
        character is >= '0' and <= '9';
}
