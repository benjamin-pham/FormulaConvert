using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>Fractions and binomials: <c>\frac</c> family, <c>\binom</c> family, <c>\nicefrac</c>, <c>\pdiff</c>, and the infix <c>\over \atop \choose</c>.</summary>
internal static class FractionCommands
{
    private static readonly string[] InfixNames = ["\\over", "\\atop", "\\choose", "\\overwithdelims", "\\atopwithdelims", "\\brace", "\\brack"];

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
        t.Add("\\nicefrac", (p, name) => Fraction(p, FracKind.Skw));
        t.Add("\\pdiff", PartialDerivative);
        t.Add("\\genfrac", (p, name) => Genfrac(p));

        t.AddInfix("\\over", (p, name, left) => Infix(p, name, left, (n, d) => new Frac(n, d, FracKind.Bar)));
        t.AddInfix("\\atop", (p, name, left) => Infix(p, name, left, (n, d) => new Frac(n, d, FracKind.NoBar)));
        t.AddInfix("\\choose", (p, name, left) => Infix(p, name, left, Delimited.Binomial));
        t.AddInfix("\\overwithdelims", (p, name, left) => InfixWithDelims(p, name, left, FracKind.Bar));
        t.AddInfix("\\atopwithdelims", (p, name, left) => InfixWithDelims(p, name, left, FracKind.NoBar));
        t.AddInfix("\\brace", (p, name, left) => Infix(p, name, left, (n, d) => new Delimited("{", "}", "", [new Frac(n, d, FracKind.NoBar)])));
        t.AddInfix("\\brack", (p, name, left) => Infix(p, name, left, (n, d) => new Delimited("[", "]", "", [new Frac(n, d, FracKind.NoBar)])));
    }

    private static FormulaNode Genfrac(LatexParser p)
    {
        int at = p.Peek(-1).Position;
        string left = ReadGenfracDelim(p);
        string right = ReadGenfracDelim(p);
        string thickness = ReadRaw(p).Trim();
        string mathstyle = ReadRaw(p).Trim();
        var num = p.ReadArgument();
        var den = p.ReadArgument();

        if (mathstyle.Length > 0)
            p.Warn("lossy", "\\genfrac mathstyle is ignored (OMML does not distinguish the style)", at);

        var kind = thickness == "0pt" ? FracKind.NoBar : FracKind.Bar;
        FormulaNode frac = new Frac(num, den, kind);

        if (left.Length == 0 && right.Length == 0)
            return frac;

        return new Delimited(left, right, "", [frac]);
    }

    private static string ReadGenfracDelim(LatexParser p)
    {
        if (p.Peek().Kind == TokenKind.BeginGroup)
        {
            var raw = p.ReadRawGroup().Trim();
            if (raw is "" or ".") return "";
            if (raw is "\\{" or "{") return "{";
            if (raw is "\\}" or "}") return "}";
            if (raw is "\\|" or "\\Vert") return "‖";
            if (raw is "\\vert" or "|") return "|";
            if (raw is "\\lbrack" or "[") return "[";
            if (raw is "\\rbrack" or "]") return "]";
            if (raw is "(" or ")") return raw;
            if (SymbolTable.TryGetCodepoint(raw, out var cp))
                return DelimiterCommands.Normalize(cp);
            return raw;
        }
        return DelimiterCommands.ReadDelimiter(p, "\\genfrac");
    }

    private static string ReadRaw(LatexParser p)
    {
        var t = p.Peek();
        if (t.Kind == TokenKind.BeginGroup) return p.ReadRawGroup();
        if (!p.HasArgument) throw p.Error("missing argument");
        return p.Next().Text;
    }

    private static FormulaNode InfixWithDelims(LatexParser p, string name, List<FormulaNode> left, FracKind kind)
    {
        int second = FindSecondInfix(p);
        if (second >= 0) throw p.Error("ambiguous " + name, second);
        var open = DelimiterCommands.ReadDelimiter(p, name);
        var close = DelimiterCommands.ReadDelimiter(p, name);
        var num = LatexParser.Rowify([.. left]);
        var den = p.ParseRowRest();
        var frac = new Frac(num, den, kind);
        return (open.Length == 0 && close.Length == 0) ? frac : new Delimited(open, close, "", [frac]);
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
                case TokenKind.Command when t.Text is "\\left" or "\\mleft" or "\\begin":
                    depth++;
                    break;
                case TokenKind.Command when t.Text == "\\middle" && depth == 0:
                    return -1;
                case TokenKind.Command when t.Text is "\\right" or "\\mright" or "\\end":
                    if (depth == 0) return -1;
                    depth--;
                    break;
                case TokenKind.Command when depth == 0 && Array.IndexOf(InfixNames, t.Text) >= 0:
                    return t.Position;
            }
        }
    }
}
