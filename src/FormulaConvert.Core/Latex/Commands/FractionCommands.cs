using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>Fractions and binomials: <c>\frac</c> family, <c>\binom</c> family, <c>\nicefrac</c>, <c>\pdiff</c>, and the infix <c>\over \atop \choose</c>.</summary>
internal static class FractionCommands
{
    private static readonly string[] InfixNames = ["\\over", "\\atop", "\\choose"];

    public static void Register(CommandTable t)
    {
        t.Add("\\frac", (p, name) => Fraction(p, FracKind.Bar));
        // OMML does not distinguish display/text style, so these read like \frac.
        t.Add("\\dfrac", (p, name) => Fraction(p, FracKind.Bar, name));
        t.Add("\\tfrac", (p, name) => Fraction(p, FracKind.Bar, name));
        t.Add("\\cfrac", Continued);
        t.Add("\\binom", (p, name) => Binomial(p));
        t.Add("\\dbinom", (p, name) => Binomial(p, name));
        t.Add("\\tbinom", (p, name) => Binomial(p, name));
        t.Add("\\nicefrac", (p, name) => Fraction(p, FracKind.Lin));
        t.Add("\\pdiff", PartialDerivative);

        t.AddInfix("\\over", (p, name, left) => Infix(p, name, left, (n, d) => new Frac(n, d, FracKind.Bar)));
        t.AddInfix("\\atop", (p, name, left) => Infix(p, name, left, (n, d) => new Frac(n, d, FracKind.NoBar)));
        t.AddInfix("\\choose", (p, name, left) => Infix(p, name, left, Delimited.Binomial));
    }

    private static FormulaNode Fraction(LatexParser p, FracKind kind, string? lossyName = null)
    {
        int at = p.Peek(-1).Position;
        var num = p.ReadArgument();
        var den = p.ReadArgument();
        if (lossyName != null) WarnStyle(p, lossyName, "\\frac", at);
        return new Frac(num, den, kind);
    }

    /// <summary><c>\cfrac[l|r]{n}{d}</c>: the alignment option has no OMML counterpart and is dropped.</summary>
    private static FormulaNode Continued(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position;
        p.ReadOptionalArgument();
        var num = p.ReadArgument();
        var den = p.ReadArgument();
        WarnStyle(p, name, "\\frac", at);
        return new Frac(num, den, FracKind.Bar);
    }

    private static FormulaNode Binomial(LatexParser p, string? lossyName = null)
    {
        int at = p.Peek(-1).Position;
        var top = p.ReadArgument();
        var bottom = p.ReadArgument();
        if (lossyName != null) WarnStyle(p, lossyName, "\\binom", at);
        return Delimited.Binomial(top, bottom);
    }

    /// <summary><c>\pdiff{f}{x}</c> is <c>\frac{\partial f}{\partial x}</c>.</summary>
    private static FormulaNode PartialDerivative(LatexParser p, string name)
    {
        var f = p.ReadArgument();
        var x = p.ReadArgument();
        var partial = LatexParser.CharRun("\u2202");
        return new Frac(new FormulaRow([partial, f]), new FormulaRow([partial, x]), FracKind.Bar);
    }

    private static void WarnStyle(LatexParser p, string name, string target, int position) =>
        p.Warn("lossy", $"{name} is written as {target} (OMML does not distinguish the style)", position);

    /// <summary>The items read so far in the row form the numerator; the rest of the row is the denominator.</summary>
    private static FormulaNode Infix(LatexParser p, string name, List<FormulaNode> left, Func<FormulaNode, FormulaNode, FormulaNode> build)
    {
        int second = FindSecondInfix(p);
        if (second >= 0) throw p.Error("ambiguous \\over", second);
        var num = LatexParser.Rowify([.. left]); // copy: Rowify keeps the list it is given
        var den = p.ParseRowRest();
        return build(num, den);
    }

    /// <summary>Position of another infix command later in the same row (outside braces), or -1.</summary>
    private static int FindSecondInfix(LatexParser p)
    {
        int depth = 0;
        for (int i = 0; ; i++)
        {
            var t = p.Peek(i);
            switch (t.Kind)
            {
                case TokenKind.EndOfInput:
                    return -1;
                case TokenKind.BeginGroup:
                    depth++;
                    break;
                case TokenKind.EndGroup:
                    if (depth == 0) return -1;
                    depth--;
                    break;
                case TokenKind.Ampersand or TokenKind.NewLine when depth == 0:
                    return -1;
                case TokenKind.Command when t.Text is "\\left" or "\\begin":
                    depth++;
                    break;
                case TokenKind.Command when t.Text == "\\middle" && depth == 0:
                    return -1;
                case TokenKind.Command when t.Text is "\\right" or "\\end":
                    if (depth == 0) return -1;
                    depth--;
                    break;
                case TokenKind.Command when depth == 0 && Array.IndexOf(InfixNames, t.Text) >= 0:
                    return t.Position;
            }
        }
    }
}
