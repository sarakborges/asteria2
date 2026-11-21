using Asteria.Content.Registries;

namespace Asteria.Serialization;

/// <summary>
/// The result of a serialization operation: either a complete value or an error
/// that may still carry a usable partial value.
/// </summary>
public abstract class DataResult<T>
{
    public abstract Lifecycle Lifecycle { get; }
    public abstract bool IsSuccess { get; }
    public bool IsError => !IsSuccess;
    public abstract bool HasResultOrPartial { get; }
    public abstract string? ErrorMessage { get; }

    public abstract bool TryGetResult(out T value);
    public abstract bool TryGetPartial(out T value);

    public bool TryGetValueOrPartial(out T value, Action<string>? onError = null)
    {
        if (TryGetResult(out value))
            return true;

        string? message = ErrorMessage;
        if (message is not null)
            onError?.Invoke(message);

        return TryGetPartial(out value);
    }

    public T GetOrThrow(Func<string, Exception>? exceptionFactory = null)
    {
        if (TryGetResult(out T value))
            return value;

        string message = ErrorMessage ?? "Serialization operation failed.";
        throw exceptionFactory?.Invoke(message) ?? new InvalidOperationException(message);
    }

    public T GetPartialOrThrow(Func<string, Exception>? exceptionFactory = null)
    {
        if (TryGetValueOrPartial(out T value))
            return value;

        string message = ErrorMessage ?? "Serialization operation failed without a partial result.";
        throw exceptionFactory?.Invoke(message) ?? new InvalidOperationException(message);
    }

    public abstract DataResult<TResult> Map<TResult>(Func<T, TResult> function);

    /// <summary>
    /// Applies the transformation to a complete or partial value. Existing and subsequent
    /// errors are retained and combined, matching DataFixerUpper's partial-result semantics.
    /// </summary>
    public abstract DataResult<TResult> FlatMap<TResult>(
        Func<T, DataResult<TResult>> function);

    public abstract DataResult<T> SetPartial(T partialValue);
    public abstract DataResult<T> MapError(Func<string, string> function);
    public abstract DataResult<T> SetLifecycle(Lifecycle lifecycle);

    public DataResult<T> AddLifecycle(Lifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        return SetLifecycle(Lifecycle.Add(lifecycle));
    }

    public DataResult<T> IfSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (TryGetResult(out T value))
            action(value);
        return this;
    }

    public DataResult<T> IfError(Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (ErrorMessage is string message)
            action(message);
        return this;
    }

    public DataResult<T> PromotePartial(Action<string> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        if (IsSuccess || !TryGetPartial(out T partial))
            return this;

        onError(ErrorMessage ?? "Serialization operation failed.");
        return Success(partial, Lifecycle);
    }

    /// <summary>Combines two results while preserving partial values and accumulating errors.</summary>
    public DataResult<TResult> Apply2<TSecond, TResult>(
        Func<T, TSecond, TResult> function,
        DataResult<TSecond> second)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(second);

        Lifecycle lifecycle = Lifecycle.Add(second.Lifecycle);
        bool hasFirst = TryGetResult(out T first) || TryGetPartial(out first);
        bool hasSecond = second.TryGetResult(out TSecond secondValue) ||
                         second.TryGetPartial(out secondValue);

        if (IsSuccess && second.IsSuccess)
            return Success(function(first, secondValue), lifecycle);

        string message = CombineErrorMessages(this, second);
        if (hasFirst && hasSecond)
            return Error(() => message, function(first, secondValue), lifecycle);

        return Error(() => message, lifecycle);
    }

    public static DataResult<T> Success(
        T result,
        Lifecycle? lifecycle = null)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        return new SuccessResult(result, lifecycle ?? Lifecycle.Experimental());
    }

    public static DataResult<T> Error(
        Func<string> message,
        Lifecycle? lifecycle = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new ErrorResult(message, false, default!, lifecycle ?? Lifecycle.Experimental());
    }

    public static DataResult<T> Error(
        Func<string> message,
        T partialValue,
        Lifecycle? lifecycle = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (partialValue is null)
            throw new ArgumentNullException(nameof(partialValue));

        return new ErrorResult(message, true, partialValue, lifecycle ?? Lifecycle.Experimental());
    }

    public static DataResult<T> Error(
        string message,
        Lifecycle? lifecycle = null) =>
        Error(() => message, lifecycle);

    public static DataResult<T> Error(
        string message,
        T partialValue,
        Lifecycle? lifecycle = null) =>
        Error(() => message, partialValue, lifecycle);

    public static string AppendMessages(string first, string second) =>
        first + "; " + second;

    private static string CombineErrorMessages<TFirst, TSecond>(
        DataResult<TFirst> first,
        DataResult<TSecond> second)
    {
        string? firstMessage = first.ErrorMessage;
        string? secondMessage = second.ErrorMessage;

        if (firstMessage is null)
            return secondMessage ?? "Serialization operation failed.";
        if (secondMessage is null)
            return firstMessage;

        return AppendMessages(firstMessage, secondMessage);
    }

    private sealed class SuccessResult : DataResult<T>
    {
        public T Value { get; }
        public override Lifecycle Lifecycle { get; }
        public override bool IsSuccess => true;
        public override bool HasResultOrPartial => true;
        public override string? ErrorMessage => null;

        public SuccessResult(T value, Lifecycle lifecycle)
        {
            Value = value;
            Lifecycle = lifecycle;
        }

        public override bool TryGetResult(out T value)
        {
            value = Value;
            return true;
        }

        public override bool TryGetPartial(out T value)
        {
            value = default!;
            return false;
        }

        public override DataResult<TResult> Map<TResult>(Func<T, TResult> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            return Success(function(Value), Lifecycle);
        }

        public override DataResult<TResult> FlatMap<TResult>(
            Func<T, DataResult<TResult>> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            return function(Value).AddLifecycle(Lifecycle);
        }

        public override DataResult<T> SetPartial(T partialValue) => this;

        public override DataResult<T> MapError(Func<string, string> function) => this;

        public override DataResult<T> SetLifecycle(Lifecycle lifecycle)
        {
            ArgumentNullException.ThrowIfNull(lifecycle);
            return ReferenceEquals(Lifecycle, lifecycle)
                ? this
                : Success(Value, lifecycle);
        }

        public override string ToString() => $"DataResult.Success[{Value}]";
    }

    private sealed class ErrorResult : DataResult<T>
    {
        private readonly Func<string> _message;
        private readonly bool _hasPartial;
        private readonly T _partialValue;

        public override Lifecycle Lifecycle { get; }
        public override bool IsSuccess => false;
        public override bool HasResultOrPartial => _hasPartial;
        public override string? ErrorMessage => _message();

        public ErrorResult(
            Func<string> message,
            bool hasPartial,
            T partialValue,
            Lifecycle lifecycle)
        {
            _message = message ?? throw new ArgumentNullException(nameof(message));
            _hasPartial = hasPartial;
            _partialValue = partialValue;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public override bool TryGetResult(out T value)
        {
            value = default!;
            return false;
        }

        public override bool TryGetPartial(out T value)
        {
            value = _partialValue;
            return _hasPartial;
        }

        public override DataResult<TResult> Map<TResult>(Func<T, TResult> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            return _hasPartial
                ? Error(_message, function(_partialValue), Lifecycle)
                : Error(_message, Lifecycle);
        }

        public override DataResult<TResult> FlatMap<TResult>(
            Func<T, DataResult<TResult>> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            if (!_hasPartial)
                return Error<TResult>(_message, Lifecycle);

            DataResult<TResult> next = function(_partialValue);
            Lifecycle lifecycle = Lifecycle.Add(next.Lifecycle);
            if (next.TryGetResult(out TResult nextValue))
                return Error(_message, nextValue, lifecycle);

            string nextMessage = next.ErrorMessage ?? "Serialization operation failed.";
            string combinedMessage = AppendMessages(_message(), nextMessage);
            return next.TryGetPartial(out nextValue)
                ? Error(() => combinedMessage, nextValue, lifecycle)
                : Error(() => combinedMessage, lifecycle);
        }

        public override DataResult<T> SetPartial(T partialValue)
        {
            if (partialValue is null)
                throw new ArgumentNullException(nameof(partialValue));

            return new ErrorResult(_message, true, partialValue, Lifecycle);
        }

        public override DataResult<T> MapError(Func<string, string> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            return new ErrorResult(() => function(_message()), _hasPartial, _partialValue, Lifecycle);
        }

        public override DataResult<T> SetLifecycle(Lifecycle lifecycle)
        {
            ArgumentNullException.ThrowIfNull(lifecycle);
            return ReferenceEquals(Lifecycle, lifecycle)
                ? this
                : new ErrorResult(_message, _hasPartial, _partialValue, lifecycle);
        }

        public override string ToString() =>
            $"DataResult.Error['{_message()}'{(_hasPartial ? $": {_partialValue}" : string.Empty)}]";
    }
}
