using System.Threading;

namespace Asteria.Content.Storage;

/// <summary>
/// Detects concurrent access using the semaphore-based check-and-lock protocol
/// used by Minecraft's ThreadingDetector.
/// </summary>
public sealed class ThreadingDetector
{
    private readonly string _name;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _stackTraceLock = new();
    private Thread? _threadThatFailedToAcquire;
    private InvalidOperationException? _fullException;

    public ThreadingDetector(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void CheckAndLock()
    {
        lock (_stackTraceLock)
        {
            if (_lock.Wait(0))
            {
                return;
            }

            _threadThatFailedToAcquire = Thread.CurrentThread;
        }

        _lock.Wait();
        throw _fullException ?? CreateThreadingException(Thread.CurrentThread);
    }

    public void CheckAndUnlock()
    {
        lock (_stackTraceLock)
        {
            Thread? failedThread = _threadThatFailedToAcquire;
            if (failedThread is not null)
            {
                _fullException = CreateThreadingException(failedThread);
                _lock.Release();
                throw _fullException;
            }

            _lock.Release();
        }
    }

    private InvalidOperationException CreateThreadingException(Thread failedThread) =>
        MakeThreadingException(_name, failedThread);

    public static InvalidOperationException MakeThreadingException(
        string name,
        Thread? threadThatFailedToAcquire)
    {
        ArgumentNullException.ThrowIfNull(name);

        var threads = new List<Thread> { Thread.CurrentThread };
        if (threadThatFailedToAcquire is not null)
        {
            threads.Add(threadThatFailedToAcquire);
        }

        string threadDumps = string.Join(
            Environment.NewLine,
            threads.Select(thread =>
            {
                string threadName = thread.Name ?? $"Thread {thread.ManagedThreadId}";
                string stackTrace = ReferenceEquals(thread, Thread.CurrentThread)
                    ? Environment.StackTrace
                    : thread.StackTrace ?? string.Empty;
                return $"{threadName}:{Environment.NewLine}" +
                    (string.IsNullOrEmpty(stackTrace)
                        ? "\t<stack trace unavailable>"
                        : stackTrace);
            }));

        string error = $"Accessing {name} from multiple threads";
        return new InvalidOperationException(
            $"{error}{Environment.NewLine}Thread dumps:{Environment.NewLine}{threadDumps}",
            new InvalidOperationException(error));
    }
}
