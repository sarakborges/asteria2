namespace Asteria.World.Level;

/// <summary>
/// Represents either a successful chunk operation or a lazily described failure.
/// Port of Minecraft Java 26.3 ChunkResult.
/// </summary>
public interface ChunkResult<T>
{
    static ChunkResult<T> Of(T value) => new Success<T>(value);

    static ChunkResult<T> Error(string error) =>
        Error(() => error);

    static ChunkResult<T> Error(Func<string> errorSupplier) =>
        new Failure<T>(errorSupplier);

    bool IsSuccess();

    T? OrElse(T? fallback);

    string? GetError();

    ChunkResult<T> IfSuccess(Action<T> consumer);

    ChunkResult<TResult> Map<TResult>(Func<T, TResult> mapper);

    T OrElseThrow(Func<Exception> exceptionSupplier);

    static TResult? OrElse<TResult>(
        ChunkResult<TResult> result,
        TResult? fallback)
    {
        TResult? value = result.OrElse(default);
        return value is not null ? value : fallback;
    }
}

public sealed record Failure<T>(Func<string> ErrorSupplier) : ChunkResult<T>
{
    public bool IsSuccess() => false;

    public T? OrElse(T? fallback) => fallback;

    public string GetError() => ErrorSupplier();

    public ChunkResult<T> IfSuccess(Action<T> consumer) => this;

    public ChunkResult<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new Failure<TResult>(ErrorSupplier);

    public T OrElseThrow(Func<Exception> exceptionSupplier) =>
        throw exceptionSupplier();
}

public sealed record Success<T>(T Value) : ChunkResult<T>
{
    public bool IsSuccess() => true;

    public T OrElse(T? fallback) => Value;

    public string? GetError() => null;

    public ChunkResult<T> IfSuccess(Action<T> consumer)
    {
        consumer(Value);
        return this;
    }

    public ChunkResult<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new Success<TResult>(mapper(Value));

    public T OrElseThrow(Func<Exception> exceptionSupplier) => Value;
}
