using FormulaConvert.Ast;
using FormulaConvert.Data;
using FormulaConvert.Latex.Commands;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary>Writes flattened row items; consecutive runs of equal style share one style command.</summary>
    private static void WriteItems(List<FormulaNode> items, LatexBuilder b)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not Run first)
            {
                WriteNonRun(items, i, b);
                continue;
            }
            int end = i + 1;
            while (end < items.Count && items[end] is Run next && next.Style == first.Style) end++;
            WriteStyledRuns(items.GetRange(i, end - i).Cast<Run>().ToList(), b);
            i = end - 1;
        }
    }

    /// <summary>
    /// A non-run item. An empty base or operand is written explicitly when it would otherwise read back as attached to
    /// a sibling: <c>a{{}}^2 b</c> (<c>{}^2 b</c> would be a prefix script), <c>\sin{}x</c>, <c>\sum_a{}x</c>.
    /// </summary>
    private static void WriteNonRun(List<FormulaNode> items, int i, LatexBuilder b)
    {
        var item = items[i];
        bool last = i == items.Count - 1;
        if (item is Script s && i > 0 && Flatten(s.Base).Count == 0)
        {
            b.Text(last ? "{}" : "{{}}");
            WriteScripts(s.Sub, s.Sup, b);
            return;
        }
        WriteNode(item, b);
        bool emptyOperand = item switch
        {
            Nary n => Flatten(n.Body).Count == 0,
            FunctionApply f => Flatten(f.Arg).Count == 0,
            _ => false,
        };
        if (emptyOperand && !last) b.Text("{}");
    }

    /// <summary>Runs of one style: bare for the default style, <c>\text{…}</c> for normal text, else Sty command outside Scr command.</summary>
    private static void WriteStyledRuns(IReadOnlyList<Run> runs, LatexBuilder b)
    {
        var style = runs[0].Style;
        if (style.Normal)
        {
            b.Command("\\text");
            b.Text("{");
            foreach (var rune in string.Concat(runs.Select(r => r.Text)).EnumerateRunes())
                WriteTextChar(rune, b);
            b.Text("}");
            return;
        }
        int open = 0;
        if (style.Sty != Sty.Italic)
        {
            b.Command(StyleCommands.StyCommands.First(x => x.Sty == style.Sty).Command);
            b.Text("{");
            open++;
        }
        if (style.Scr != Scr.Roman)
        {
            b.Command(StyleCommands.ScrCommands.First(x => x.Scr == style.Scr).Command);
            b.Text("{");
            open++;
        }
        var text = runs.Count == 1 ? runs[0].Text : string.Concat(runs.Select(r => r.Text));
        WriteMathText(text, b);
        if (open > 0) b.Text(new string('}', open));
    }

    /// <summary>Writes math run text: pairs of U+2003 merge into <c>\qquad</c>, spaces become canonical commands, other characters go to <see cref="WriteChar"/>.</summary>
    internal static void WriteMathText(string text, LatexBuilder b)
    {
        var runes = text.EnumerateRunes().ToList();
        for (int i = 0; i < runes.Count; i++)
        {
            var rune = runes[i];
            if (rune.Value == 0x2003)
            {
                if (i + 1 < runes.Count && runes[i + 1].Value == 0x2003)
                {
                    b.Command("\\qquad");
                    i++;
                }
                else
                {
                    b.Command("\\quad");
                }
            }
            else if (SpaceTable.TryGetCommand(rune.Value, out var command))
            {
                b.Command(command);
            }
            else
            {
                WriteChar(rune, b);
            }
        }
    }

    /// <summary>One character of normal text: special characters escaped, everything else literal.</summary>
    private static void WriteTextChar(System.Text.Rune rune, LatexBuilder b)
    {
        if (rune.Value == '\\') b.Command("\\backslash").Text("{}");
        else if (rune.Value < 0x80 && EscapedChars.Contains((char)rune.Value)) b.Command("\\" + (char)rune.Value);
        else b.Text(rune.ToString());
    }
}
