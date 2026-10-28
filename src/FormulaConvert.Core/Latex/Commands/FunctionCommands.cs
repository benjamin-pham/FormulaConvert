using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>Named functions (<c>\sin</c>, <c>\lim</c>, …) and <c>\operatorname{name}</c>.</summary>
internal static class FunctionCommands
{
    public static void Register(CommandTable t)
    {
        foreach (var name in FunctionTable.All)
            t.Add(name, (p, n) => ParseFunction(p, n[1..], n));
        t.Add("\\operatorname", (p, n) =>
        {
            if (p.Peek() is { Kind: TokenKind.Char, Text: "*" }) p.Next(); // \operatorname*: limits placement is not modelled, same as the plain form
            int pos = p.Position;
            var raw = p.ReadRawGroup().Trim();
            if (raw.Length == 0) throw p.Error("empty function name", pos);
            return ParseFunction(p, raw, "\\" + raw);
        });
    }

    private static FormulaNode ParseFunction(LatexParser p, string text, string command)
    {
        bool? ignored = null;
        var (sub, sup) = NaryCommands.ReadScripts(p, ref ignored);
        var name = new Run(text, new RunStyle(Sty.Plain));
        FormulaNode head = (sub, sup) switch
        {
            (null, null) => name,
            (not null, null) when FunctionTable.BoundsFunctions.Contains(command) => new Limit(name, sub, false),
            _ => new Script(name, sub, sup),
        };
        return new FunctionApply(head, NaryCommands.ReadOperand(p, true));
    }
}
