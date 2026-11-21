namespace Asteria.Core;

/// <summary>
/// Real-valued spatial position contract used by vector distance queries.
/// </summary>
public interface Position
{
    double GetX();
    double GetY();
    double GetZ();
}
