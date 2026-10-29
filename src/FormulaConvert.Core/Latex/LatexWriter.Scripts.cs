using System.Text;
using FormulaConvert.Ast;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary>Characters that cannot be written bare as a script argument or base without braces.</summary>
    private const string NeedsBraces = "#$%&_{}\\^~'[] ";

    private static void WriteScript(Script s, LatexBuilder b)
    {
        WriteScriptBase(s.Base, b);
        WriteScripts(s.Sub, s.Sup, b);
    }

    /// <summary><c>{}_a^b x</c>: an empty group carries the scripts, then the base.</summary>
    private static void WritePreScript(PreScript s, LatexBuilder b)
    {
        b.Text("{}");
        WriteScripts(s.Sub, s.Sup, b);
        if (s.Base is Script || IsSingleAtom(s.Base))
            WriteNode(s.Base, b);
        else
            WriteGroup(s.Base, b);
    }

    private static void WriteScripts(FormulaNode? sub, FormulaNode? sup, LatexBuilder b)
    {
        if (sub != null)
        {
            b.Text("_");
            WriteScriptArgument(sub, b);
        }
        if (sup != null)
        {
            b.Text("^");
            if (TryGetPrimeCount(sup, out int primes))
            {
                b.Text("{");
                for (int i = 0; i < primes; i++) b.Command("\\prime");
                b.Text("}");
            }
            else
            {
                WriteScriptArgument(sup, b);
            }
        }
    }

    /// <summary>The base of a script: nothing when empty, braces unless it reads back as one atom.</summary>
    private static void WriteScriptBase(FormulaNode node, LatexBuilder b)
    {
        var flat = Flatten(node);
        if (flat.Count == 0) return;
        if (flat.Count == 1 && flat[0] is not (Script or PreScript or Nary or FunctionApply or Frac { Kind: FracKind.Lin }) && !IsLabeledBrace(flat[0]) && (flat[0] is not Run r || RuneCount(r) == 1))
            WriteNode(flat[0], b);
        else
            WriteGroup(node, b);
    }

    private static bool IsLabeledBrace(FormulaNode node)
    {
        if (node is not Limit lim) return false;
        var baseNode = Flatten(lim.Base) is [var single] ? single : lim.Base;
        return lim.Upper
            ? baseNode is GroupChar { Chr: "⏞", Top: true, VertJcTop: false }
            : baseNode is GroupChar { Chr: "⏟", Top: false, VertJcTop: false };
    }

    private static void WriteScriptArgument(FormulaNode node, LatexBuilder b)
    {
        if (IsSingleAtom(node) && !IsNeedsBraces(node))
        {
            WriteNode(node, b);
            b.AfterUnbracedScript();
        }
        else
        {
            WriteGroup(node, b);
        }
    }

    /// <summary>A single-character <see cref="Run"/> (possibly inside one-item rows).</summary>
    private static bool IsSingleAtom(FormulaNode node)
    {
        var flat = Flatten(node);
        return flat.Count == 1 && flat[0] is Run r && RuneCount(r) == 1;
    }

    private static bool IsNeedsBraces(FormulaNode node) =>
        Flatten(node)[0] is Run { Text.Length: 1 } r && NeedsBraces.Contains(r.Text[0]);

    private static int RuneCount(Run r) => r.Text.EnumerateRunes().Count();

    /// <summary>True when the node is only prime characters (U+2032, or U+2033 counting as two).</summary>
    private static bool TryGetPrimeCount(FormulaNode node, out int count)
    {
        count = 0;
        var flat = Flatten(node);
        if (flat.Count == 0) return false;
        foreach (var item in flat)
        {
            if (item is not Run { Text.Length: > 0 } run) return false;
            foreach (var c in run.Text)
            {
                if (c == '′') count++;
                else if (c == '″') count += 2;
                else return false;
            }
        }
        return true;
    }

    private static List<FormulaNode> Flatten(FormulaNode node)
    {
        var items = new List<FormulaNode>();
        FlattenInto(node, items);
        return items;
    }

    private static void FlattenInto(FormulaNode node, List<FormulaNode> items)
    {
        if (node is FormulaRow row)
            foreach (var item in row.Items) FlattenInto(item, items);
        else
            items.Add(node);
    }

    private static void WriteOptionalArgument(FormulaNode node, LatexBuilder b)
    {
        var inner = new LatexBuilder(b.Nesting);
        WriteNode(node, inner);
        var text = inner.ToString();
        // A ] outside braces would end the optional argument early: brace the whole text.
        b.Text(HasUnbracedCloseBracket(text) ? "[{" + text + "}]" : "[" + text + "]");
    }

    private static void WriteRadical(Radical r, LatexBuilder b)
    {
        b.Command("\\sqrt");
        if (r.Degree != null)
            WriteOptionalArgument(r.Degree, b);
        WriteGroup(r.Body, b);
    }

    private static bool HasUnbracedCloseBracket(string latex)
    {
        int depth = 0;
        for (int i = 0; i < latex.Length; i++)
        {
            switch (latex[i])
            {
                case '\\': i++; break; // skip the escaped character (\} \{ …)
                case '{': depth++; break;
                case '}': depth--; break;
                case ']' when depth == 0: return true;
            }
        }
        return false;
    }
}
