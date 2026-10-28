using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// <c>\not</c>: negates the next relation by replacing the pair with the precomposed negated symbol.
/// Any other following token is left untouched and one <c>out-of-scope</c> diagnostic is added.
/// </summary>
internal static class NegationCommands
{
    /// <summary>Relation (character or command) to the code point of its negated form.</summary>
    private static readonly Dictionary<string, int> Negated = new(StringComparer.Ordinal)
    {
        ["="] = 0x2260, ["<"] = 0x226E, [">"] = 0x226F,
        ["\\in"] = 0x2209, ["\\ni"] = 0x220C,
        ["\\subset"] = 0x2284, ["\\supset"] = 0x2285, ["\\subseteq"] = 0x2288, ["\\supseteq"] = 0x2289,
        ["\\exists"] = 0x2204, ["\\equiv"] = 0x2262, ["\\sim"] = 0x2241, ["\\approx"] = 0x2249, ["\\cong"] = 0x2247,
        ["\\leq"] = 0x2270, ["\\le"] = 0x2270, ["\\geq"] = 0x2271, ["\\ge"] = 0x2271,
        ["\\parallel"] = 0x2226, ["\\mid"] = 0x2224,
        ["\\vdash"] = 0x22AC, ["\\models"] = 0x22AD, ["\\vDash"] = 0x22AD,
        ["\\prec"] = 0x2280, ["\\succ"] = 0x2281,
    };

    public static void Register(CommandTable t) => t.Add("\\not", Handle);

    private static FormulaNode Handle(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position; // the command token, already consumed
        var next = p.Peek();
        if (next.Kind is TokenKind.Char or TokenKind.Command && Negated.TryGetValue(next.Text, out var cp))
        {
            p.Next();
            return LatexParser.CharRun(char.ConvertFromUtf32(cp));
        }
        p.Warn("out-of-scope", $"{name} is not supported by OMML for this symbol", at);
        return FormulaRow.Empty;
    }
}
