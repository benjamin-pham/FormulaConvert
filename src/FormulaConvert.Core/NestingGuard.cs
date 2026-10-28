using System.Xml.Linq;

namespace FormulaConvert;

/// <summary>
/// Reusable recursion limit: call <see cref="Enter"/> before descending into a nested structure and <see cref="Exit"/>
/// after it; exceeding <see cref="Limit"/> levels throws <see cref="FormulaParseException"/> "nesting too deep".
/// </summary>
internal sealed class NestingGuard
{
    public const int Limit = 1000;

    private int _depth;

    public void Enter(int position)
    {
        if (++_depth > Limit)
            throw TooDeep(position);
    }

    public void Exit() => _depth--;

    /// <summary>Checks (without recursion) that no element is nested more than <see cref="Limit"/> levels below <paramref name="root"/>.</summary>
    public static void CheckDepth(XElement root, int position)
    {
        var stack = new Stack<(XElement Element, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (e, depth) = stack.Pop();
            if (depth > Limit) throw TooDeep(position);
            foreach (var child in e.Elements()) stack.Push((child, depth + 1));
        }
    }

    private static FormulaParseException TooDeep(int position) =>
        new($"nesting too deep (more than {Limit} levels)", position);
}
