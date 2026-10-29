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
            bool starred = false;
            if (p.Peek() is { Kind: TokenKind.Char, Text: "*" })
            {
                p.Next();
                starred = true; // \operatorname*: a lone subscript goes below the name, like \lim
            }
            int pos = p.Position;
            if (p.Peek().Kind != TokenKind.BeginGroup) throw p.Error("expected {");
            var name = OperatorName(p.ReadArgument());
            if (name is Run { Text.Length: 0 }) throw p.Error("empty function name", pos);
            bool limits = starred || name is Run r && FunctionTable.BoundsFunctions.Contains("\\" + r.Text);
            return ParseFunction(p, name, limits);
        });
    }

    /// <summary>The argument of <c>\operatorname</c>, parsed as math: its runs joined into one upright run (other content kept, spaces dropped).</summary>
    private static FormulaNode OperatorName(FormulaNode arg)
    {
        var items = new List<FormulaNode>();
        Collect(arg, items);
        if (items.All(i => i is Run))
        {
            var text = string.Concat(items.Cast<Run>().Select(r => r.Text));
            var stripped = string.Concat(text.EnumerateRunes().Where(r => !SpaceTable.IsSpace(r.Value)).Select(r => r.ToString()));
            return new Run(stripped, new RunStyle(Sty.Plain));
        }
        return arg;

        static void Collect(FormulaNode node, List<FormulaNode> items)
        {
            if (node is FormulaRow row) foreach (var item in row.Items) Collect(item, items);
            else items.Add(node);
        }
    }

    private static FormulaNode ParseFunction(LatexParser p, string text, string command) =>
        ParseFunction(p, new Run(text, new RunStyle(Sty.Plain)), FunctionTable.BoundsFunctions.Contains(command));

    private static FormulaNode ParseFunction(LatexParser p, FormulaNode name, bool limits)
    {
        bool? ignored = null;
        var (sub, sup) = NaryCommands.ReadScripts(p, ref ignored);
        FormulaNode head = (sub, sup) switch
        {
            (null, null) => name,
            (not null, null) when limits => new Limit(name, sub, false),
            _ => new Script(name, sub, sup),
        };
        return new FunctionApply(head, NaryCommands.ReadOperand(p, true));
    }
}
