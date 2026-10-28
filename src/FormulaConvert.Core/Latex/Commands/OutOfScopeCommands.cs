using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// Commands OMML cannot express (colors, sizes, spacing, html hooks, …): the wrapper is dropped, the kept argument
/// stays, and one <c>out-of-scope</c> diagnostic is added. Rules come from <c>outofscope.csv</c>
/// (<c>command,arity,kept_arg_index</c>); every argument except the kept one is read as raw text, not math.
/// </summary>
internal static class OutOfScopeCommands
{
    /// <summary>Commands that take a leading optional <c>[…]</c> argument.</summary>
    private static readonly HashSet<string> WithOptional = new(StringComparer.Ordinal) { "\\bbox", "\\rule", "\\raisebox" };

    public static void Register(CommandTable t)
    {
        foreach (var name in OutOfScopeTable.Names)
        {
            if (t.Has(name)) continue; // another group already gives it a meaning
            OutOfScopeTable.TryGet(name, out var rule);
            t.Add(name, (p, n) => Handle(p, n, rule.Arity, rule.KeptArg));
        }
        // `\,` cannot be a row of the comma-separated file
        if (!t.Has("\\,")) t.Add("\\,", (p, n) => Handle(p, n, 0, -1));
    }

    private static FormulaNode Handle(LatexParser p, string name, int arity, int keptArg)
    {
        int at = p.Peek(-1).Position; // the command token, already consumed
        p.Warn("out-of-scope", $"{name} is not supported by OMML", at);
        if (WithOptional.Contains(name)) p.ReadRawOptional();
        if (name == "\\hspace" && p.Peek() is { Kind: TokenKind.Char, Text: "*" }) p.Next();
        FormulaNode kept = FormulaRow.Empty;
        string? raw = null;
        for (int i = 0; i < arity; i++)
        {
            if (i == keptArg) kept = p.ReadArgument();
            else raw = ReadRaw(p);
        }
        // \ce{H2O}: the chemical formula survives as normal text
        if (name is "\\ce" or "\\pu" && !string.IsNullOrEmpty(raw))
            return new Run(raw, new RunStyle(Normal: true));
        return kept;
    }

    /// <summary>A non-math argument: a raw <c>{…}</c> group or a single token.</summary>
    private static string ReadRaw(LatexParser p)
    {
        var t = p.Peek();
        if (t.Kind == TokenKind.BeginGroup) return p.ReadRawGroup();
        if (!p.HasArgument) throw p.Error("missing argument");
        return p.Next().Text;
    }
}
