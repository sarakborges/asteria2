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

    private InvalidOperationException CreateThreadingException(Thread failedThread)
    {
        string failedThreadName = failedThread.Name ?? $"Thread {failedThread.ManagedThreadId}";
        string currentThreadName = Thread.CurrentThread.Name ?? $"Thread {Thread.CurrentThread.ManagedThreadId}";
        return new InvalidOperationException(
            $"Accessing {_name} from multiple threads. Current thread: {currentThreadName}; " +
            $"thread that failed to acquire: {failedThreadName}.{Environment.NewLine}" +
            $"Current stack:{Environment.NewLine}{Environment.StackTrace}");
    }
}
