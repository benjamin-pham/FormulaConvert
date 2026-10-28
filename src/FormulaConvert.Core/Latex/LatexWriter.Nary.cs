using System.Text;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary><c>\sum\limits_a^b x</c>: the placement modifier only when it differs from the character's default.</summary>
    private static void WriteNary(Nary n, LatexBuilder b)
    {
        bool defaultSubSup = false;
        if (NaryTable.TryGetByChar(n.Chr, out var info))
        {
            b.Command(info.Command);
            defaultSubSup = info.SubSupDefault;
        }
        else
        {
            foreach (var rune in n.Chr.EnumerateRunes()) WriteChar(rune, b);
        }
        if (n.SubSupLimits != defaultSubSup)
            b.Command(n.SubSupLimits ? "\\nolimits" : "\\limits");
        WriteScripts(n.Sub, n.Sup, b);
        WriteOperand(n.Body, false, b);
    }

    private static void WriteFunctionApply(FunctionApply f, LatexBuilder b)
    {
        var head = Flatten(f.Name);
        switch (head)
        {
            case [Script s] when TryGetName(s.Base, out var name):
                WriteFunctionName(name, b);
                WriteScripts(s.Sub, s.Sup, b);
                break;
            case [Limit l] when TryGetName(l.Base, out var name):
                // A lone lower limit on a name that is not a bounds function needs \operatorname* to read back as a limit.
                WriteFunctionName(name, b, starred: !l.Upper && !FunctionTable.BoundsFunctions.Contains("\\" + name));
                b.Text(l.Upper ? "^" : "_");
                WriteScriptArgument(l.Lim, b);
                break;
            default:
                if (TryGetName(f.Name, out var plain)) WriteFunctionName(plain, b);
                else WriteNode(f.Name, b);
                break;
        }
        WriteOperand(f.Arg, true, b);
    }

    /// <summary>The text of a function name made only of runs.</summary>
    private static bool TryGetName(FormulaNode node, out string name)
    {
        var flat = Flatten(node);
        name = "";
        if (flat.Count == 0 || flat.Any(i => i is not Run)) return false;
        name = string.Concat(flat.Cast<Run>().Select(r => r.Text));
        return name.Length > 0;
    }

    /// <summary>
    /// A known function as its command, any other name (or a starred one) as <c>\operatorname{name}</c> with each
    /// character escaped.
    /// </summary>
    private static void WriteFunctionName(string name, LatexBuilder b, bool starred = false)
    {
        if (!starred && FunctionTable.Contains("\\" + name))
        {
            b.Command("\\" + name);
            return;
        }
        b.Command("\\operatorname");
        b.Text(starred ? "*{" : "{");
        foreach (var rune in name.EnumerateRunes()) WriteChar(rune, b);
        b.Text("}");
    }

    /// <summary>
    /// The operand of an operator or function: bare when it reads back as one atom (or, for functions, a bracketed
    /// run sequence), otherwise in braces; nothing when empty.
    /// </summary>
    private static void WriteOperand(FormulaNode body, bool function, LatexBuilder b)
    {
        var flat = Flatten(body);
        if (flat.Count == 0) return;
        bool bare = function && IsBracketed(flat)
            || flat.Count == 1 && flat[0] switch
            {
                Run r => RuneCount(r) == 1 && r.Text != " " && !(function && r.Text is "(" or "["),
                PreScript => false,
                Script s => Flatten(s.Base).Count > 0,
                _ => true,
            };
        if (bare) WriteNode(body, b);
        else WriteGroup(body, b);
    }

    /// <summary>Runs forming text that starts with <c>(</c>/<c>[</c> and ends with its matching closing character.</summary>
    private static bool IsBracketed(List<FormulaNode> flat)
    {
        if (flat.Any(i => i is not Run)) return false;
        var text = string.Concat(flat.Cast<Run>().Select(r => r.Text));
        if (text.Length < 2 || text[0] is not ('(' or '[')) return false;
        char open = text[0], close = open == '(' ? ')' : ']';
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close && --depth == 0) return i == text.Length - 1;
        }
        return false;
    }
}
