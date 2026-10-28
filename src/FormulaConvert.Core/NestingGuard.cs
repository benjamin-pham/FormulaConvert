using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace FormulaConvert;

/// <summary>
/// Reusable recursion limit: call <see cref="Enter"/> before descending into a nested structure and <see cref="Exit"/>
/// after it; exceeding the limit, or running low on stack, throws <see cref="FormulaParseException"/> "nesting too deep".
/// </summary>
/// <remarks>
/// The LaTeX parser allows <see cref="LatexLimit"/> levels. One LaTeX level becomes at most five XML levels in OMML
/// (<c>\begin{pmatrix}</c> → <c>m:d/m:e/m:m/m:mr/m:e</c>), so the OMML reader (and the LaTeX writer, whose AST depth
/// is bounded by the XML depth) allow <see cref="XmlLimit"/> levels (5 × <see cref="LatexLimit"/> plus slack for
/// <c>m:oMath</c>, <c>m:r</c> and <c>m:t</c>): whatever LaTeX → OMML produces, OMML → LaTeX accepts. Both limits fit
/// a 1 MB stack (the Windows default); the stack check turns any remaining overflow into "nesting too deep".
/// </remarks>
internal sealed class NestingGuard
{
    public const int LatexLimit = 200;
    public const int XmlLimit = 1024;

    private readonly int _limit;
    private int _depth;

    public NestingGuard(int limit = LatexLimit) => _limit = limit;

    public void Enter(int position)
    {
        if (++_depth > _limit)
            throw TooDeep(position, _limit);
        EnsureStack(position, _limit);
    }

    public void Exit() => _depth--;

    /// <summary>Throws "nesting too deep" when the current thread is close to running out of stack.</summary>
    public static void EnsureStack(int position, int limit = XmlLimit)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw TooDeep(position, limit);
    }

    /// <summary>Checks (without recursion) that no element is nested more than <see cref="XmlLimit"/> levels below <paramref name="root"/>.</summary>
    public static void CheckDepth(XElement root, int position)
    {
        var stack = new Stack<(XElement Element, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (e, depth) = stack.Pop();
            if (depth > XmlLimit) throw TooDeep(position, XmlLimit);
            foreach (var child in e.Elements()) stack.Push((child, depth + 1));
        }
    }

    internal static FormulaParseException TooDeep(int position, int limit = XmlLimit) =>
        new($"nesting too deep (more than {limit} levels)", position);
}
