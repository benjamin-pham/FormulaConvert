using FormulaConvert.Ast;

namespace FormulaConvert.Latex;

/// <summary>Attaches <c>^</c>, <c>_</c> and <c>'</c> suffixes to an atom (shared by every construct that takes scripts).</summary>
internal static class ScriptParsing
{
    private const string Prime = "′";

    private static FormulaNode ReadScriptArgument(LatexParser p, Token mark)
    {
        if (!p.HasArgument)
            throw p.Error($"missing argument for {mark.Text}", mark.Position);
        return p.ReadArgument();
    }

    /// <summary>
    /// Reads consecutive <c>^</c>/<c>_</c>/<c>'</c> after <paramref name="atom"/> (at most one superscript and one
    /// subscript, in any order) and returns the resulting <see cref="Script"/>, or the atom itself when there are none.
    /// </summary>
    public static FormulaNode Attach(LatexParser p, FormulaNode atom)
    {
        FormulaNode? sub = null, sup = null;
        while (true)
        {
            var t = p.Peek();
            switch (t.Kind)
            {
                case TokenKind.Superscript:
                    if (sup != null) throw p.Error("double superscript", t.Position);
                    p.Next();
                    sup = ReadScriptArgument(p, t);
                    break;
                case TokenKind.Subscript:
                    if (sub != null) throw p.Error("double subscript", t.Position);
                    p.Next();
                    sub = ReadScriptArgument(p, t);
                    break;
                case TokenKind.Prime:
                    if (sup != null) throw p.Error("double superscript", t.Position);
                    var primes = new List<FormulaNode>();
                    while (p.Peek().Kind == TokenKind.Prime)
                    {
                        p.Next();
                        primes.Add(LatexParser.CharRun(Prime));
                    }
                    if (p.Peek() is { Kind: TokenKind.Superscript } mark)
                    {
                        // x'^2 is x^{\prime 2}: the superscript continues after the primes.
                        p.Next();
                        primes.Add(ReadScriptArgument(p, mark));
                    }
                    sup = LatexParser.Rowify(primes);
                    break;
                default:
                    if (atom is GroupChar { Chr: "⏞", Top: true, VertJcTop: false } && sub == null && sup != null)
                        return new Limit(atom, sup, true);
                    if (atom is GroupChar { Chr: "⏟", Top: false, VertJcTop: false } && sub != null && sup == null)
                        return new Limit(atom, sub, false);
                    return sub == null && sup == null ? atom : new Script(atom, sub, sup);
            }
        }
    }
}
