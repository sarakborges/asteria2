using System.Collections.Concurrent;

namespace Asteria.Serialization.Nbt;

/// <summary>One recoverable NBT value read/write problem.</summary>
public sealed record NbtValueProblem(
    string Path,
    string Operation,
    string Message);

/// <summary>
/// Collects codec diagnostics for a compound and its nested children. Child reporters
/// share one thread-safe queue while extending the diagnostic path.
/// </summary>
public sealed class NbtProblemReporter
{
    private readonly ConcurrentQueue<NbtValueProblem> _problems;
    private readonly string _path;

    public NbtProblemReporter()
        : this(new ConcurrentQueue<NbtValueProblem>(), string.Empty)
    {
    }

    private NbtProblemReporter(
        ConcurrentQueue<NbtValueProblem> problems,
        string path)
    {
        _problems = problems;
        _path = path;
    }

    public IReadOnlyList<NbtValueProblem> Problems =>
        _problems.ToArray();

    public bool HasProblems => !_problems.IsEmpty;

    public NbtProblemReporter ForChild(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string childPath = _path.Length == 0
            ? name
            : _path + "." + name;
        return new NbtProblemReporter(_problems, childPath);
    }

    public NbtProblemReporter ForIndex(int index)
    {
        string childPath = _path + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
        return new NbtProblemReporter(_problems, childPath);
    }

    public void Report(string operation, string message)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(message);
        _problems.Enqueue(new NbtValueProblem(_path, operation, message));
    }
}
