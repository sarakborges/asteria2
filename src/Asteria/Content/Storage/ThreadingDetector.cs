using System.Threading;

namespace Asteria.Content.Storage;

/// <summary>
/// Detects concurrent access using the semaphore-based check-and-lock protocol
/// used by the reference threading detector.
/// </summary>
public sealed class ThreadingDetector
{
    private readonly string _name;
    private readonly SemaphoreSlim _lock = new(1);
    private readonly object _stackTraceLock = new();
    private Thread? _threadThatFailedToAcquire;
    private string? _stackTraceOfThreadThatFailedToAcquire;
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
            _stackTraceOfThreadThatFailedToAcquire = Environment.StackTrace;
        }

        try
        {
            _lock.Wait();
        }
        catch (ThreadInterruptedException)
        {
            // Java's Semaphore.acquire restores the interrupted status before reporting the violation.
            Thread.CurrentThread.Interrupt();
        }

        throw _fullException ?? CreateThreadingException(Thread.CurrentThread, Environment.StackTrace);
    }

    public void CheckAndUnlock()
    {
        lock (_stackTraceLock)
        {
            Thread? failedThread = _threadThatFailedToAcquire;
            if (failedThread is not null)
            {
                _fullException = CreateThreadingException(
                    failedThread,
                    _stackTraceOfThreadThatFailedToAcquire ?? string.Empty);
                _lock.Release();
                throw _fullException;
            }

            _lock.Release();
        }
    }

    private InvalidOperationException CreateThreadingException(
        Thread failedThread,
        string failedThreadStackTrace)
    {
        string currentThreadName =
            Thread.CurrentThread.Name ?? $"Thread {Thread.CurrentThread.ManagedThreadId}";
        string failedThreadName =
            failedThread.Name ?? $"Thread {failedThread.ManagedThreadId}";

        string threadDumps =
            $"{currentThreadName}:{Environment.NewLine}{Environment.StackTrace}" +
            $"{Environment.NewLine}{failedThreadName}:{Environment.NewLine}{failedThreadStackTrace}";

        string error = $"Accessing {_name} from multiple threads";
        return new InvalidOperationException(
            $"{error}{Environment.NewLine}Thread dumps:{Environment.NewLine}{threadDumps}",
            new InvalidOperationException(error));
    }
}
