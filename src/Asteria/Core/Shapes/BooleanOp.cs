namespace Asteria.Core.Shapes;

/// <summary>
/// Boolean occupancy operation used when combining two discrete voxel shapes.
/// </summary>
public sealed class BooleanOp
{
    public static readonly BooleanOp FALSE = new(static (_, _) => false);
    public static readonly BooleanOp NOT_OR = new(static (first, second) => !first && !second);
    public static readonly BooleanOp ONLY_SECOND = new(static (first, second) => second && !first);
    public static readonly BooleanOp NOT_FIRST = new(static (first, _) => !first);
    public static readonly BooleanOp ONLY_FIRST = new(static (first, second) => first && !second);
    public static readonly BooleanOp NOT_SECOND = new(static (_, second) => !second);
    public static readonly BooleanOp NOT_SAME = new(static (first, second) => first != second);
    public static readonly BooleanOp NOT_AND = new(static (first, second) => !first || !second);
    public static readonly BooleanOp AND = new(static (first, second) => first && second);
    public static readonly BooleanOp SAME = new(static (first, second) => first == second);
    public static readonly BooleanOp SECOND = new(static (_, second) => second);
    public static readonly BooleanOp CAUSES = new(static (first, second) => !first || second);
    public static readonly BooleanOp FIRST = new(static (first, _) => first);
    public static readonly BooleanOp CAUSED_BY = new(static (first, second) => first || !second);
    public static readonly BooleanOp OR = new(static (first, second) => first || second);
    public static readonly BooleanOp TRUE = new(static (_, _) => true);

    private readonly Func<bool, bool, bool> _operation;

    private BooleanOp(Func<bool, bool, bool> operation) => _operation = operation;

    public bool Apply(bool first, bool second) => _operation(first, second);
}
