using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>N-ary operators (<c>\sum</c>, <c>\int</c>, …) and the <c>\limits</c> family; also the operand and script reading shared with functions.</summary>
internal static class NaryCommands
{
    public static void Register(CommandTable t)
    {
        foreach (var info in NaryTable.All)
            t.Add(info.Command, ParseNary);
        // Stray placement modifiers (not directly after an operator or function) have no effect.
        foreach (var name in new[] { "\\limits", "\\nolimits", "\\displaylimits" })
            t.Add(name, (p, n) =>
            {
                p.Warn("lossy", $"{n} outside an operator has no effect");
                return FormulaRow.Empty;
            });
    }

    private static FormulaNode ParseNary(LatexParser p, string name)
    {
        NaryTable.TryGetByCommand(name, out var info);
        bool? subSup = null;
        var (sub, sup) = ReadScripts(p, ref subSup);
        var body = ReadOperand(p, false);
        return new Nary(info.Chr, sub, sup, body, subSup ?? info.SubSupDefault);
    }

    /// <summary>
    /// Reads <c>\limits</c>/<c>\nolimits</c>/<c>\displaylimits</c> and <c>_</c>/<c>^</c>/<c>'</c> suffixes in any order
    /// (<paramref name="subSup"/>: true after <c>\nolimits</c>, false after <c>\limits</c>, null for the default).
    /// </summary>
    internal static (FormulaNode? Sub, FormulaNode? Sup) ReadScripts(LatexParser p, ref bool? subSup)
    {
        FormulaNode? sub = null, sup = null;
        while (true)
        {
            var t = p.Peek();
            if (t.Kind == TokenKind.Command && t.Text is "\\limits" or "\\nolimits" or "\\displaylimits")
            {
                p.Next();
                subSup = t.Text switch { "\\limits" => false, "\\nolimits" => true, _ => null };
            }
            else if (t.Kind is TokenKind.Superscript or TokenKind.Subscript or TokenKind.Prime)
            {
                var s = (Script)ScriptParsing.Attach(p, FormulaRow.Empty);
                if (s.Sub != null)
                    sub = sub == null ? s.Sub : throw p.Error("double subscript", t.Position);
                if (s.Sup != null)
                    sup = sup == null ? s.Sup : throw p.Error("double superscript", t.Position);
            }
            else
            {
                return (sub, sup);
            }
        }
    }

    /// <summary>
    /// The operand: the next atom with its attached scripts (empty when none). For functions an argument starting
    /// with <c>(</c> or <c>[</c> extends through the matching closing character.
    /// </summary>
    internal static FormulaNode ReadOperand(LatexParser p, bool function)
    {
        var t = p.Peek();
        if (!p.HasArgument || t.Kind == TokenKind.Command && CommandTable.Default.TryGetInfix(t.Text, out _))
            return FormulaRow.Empty;
        if (function && (t.Kind == TokenKind.Char && t.Text == "(" || t.Kind == TokenKind.LBracket))
            return ReadBracketed(p, t.Text, t.Text == "(" ? ")" : "]");
        return ScriptParsing.Attach(p, p.ReadArgument());
    }

    private static FormulaNode ReadBracketed(LatexParser p, string open, string close)
    {
        var items = new List<FormulaNode> { LatexParser.CharRun(p.Next().Text) };
        int depth = 1;
        while (p.HasArgument)
        {
            var t = p.Peek();
            if (t.Kind == TokenKind.Command && CommandTable.Default.TryGetInfix(t.Text, out _)) break;
            bool plain = t.Kind is TokenKind.Char or TokenKind.LBracket or TokenKind.RBracket;
            if (plain && t.Text == open) depth++;
            else if (plain && t.Text == close) depth--;
            var atom = p.ReadArgument();
            if (depth == 0)
            {
                items.Add(atom);
                break;
            }
            items.Add(ScriptParsing.Attach(p, atom));
        }
        return LatexParser.Rowify(items);
    }
}
