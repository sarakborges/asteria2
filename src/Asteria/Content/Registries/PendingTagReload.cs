namespace Asteria.Content.Registries;

/// <summary>Prepared replacement of a registry's tag snapshot.</summary>
public sealed class PendingTagReload<T> where T : class
{
    private readonly Action _apply;
    internal PendingTagReload(Action apply) =>
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    public void Apply() => _apply();
}
