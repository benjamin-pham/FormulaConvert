using FormulaConvert.Ast;
using FormulaConvert.Data;
using FormulaConvert.Latex.Commands;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary>An accent as its command; a character without a command falls back to <c>\overset{chr}{body}</c>.</summary>
    private static void WriteAccent(Accent a, LatexBuilder b)
    {
        string? command = AccentTable.TryGetByChar(a.Chr, out var c) ? c
            : AccentCommands.ExtraAccents.FirstOrDefault(x => x.Chr == a.Chr).Command;
        if (command == null)
        {
            WriteOverUnder("\\overset", WriteText(a.Chr), a.Body, b);
            return;
        }
        b.Command(command);
        WriteGroup(a.Body, b);
    }

    private static void WriteBar(Bar bar, LatexBuilder b)
    {
        b.Command(bar.Top ? "\\overline" : "\\underline");
        WriteGroup(bar.Body, b);
    }

    private static void WriteGroupChar(GroupChar g, LatexBuilder b)
    {
        foreach (var (command, chr, top) in AccentCommands.GroupChars)
        {
            if (chr != g.Chr || top != g.Top) continue;
            b.Command(command);
            WriteGroup(g.Body, b);
            return;
        }
        // No command for this character: keep it as an over/under label.
        WriteOverUnder(g.Top ? "\\overset" : "\\underset", WriteText(g.Chr), g.Body, b);
    }

    /// <summary>
    /// <c>\overset{a}{x}</c>, <c>\underset{b}{x}</c>, <c>\overunderset{a}{b}{x}</c>, or an extensible arrow
    /// <c>\xrightarrow[b]{a}</c> when the base is one arrow character.
    /// </summary>
    private static void WriteLimit(Limit l, LatexBuilder b)
    {
        FormulaNode? above = null, below = null;
        var core = l.Base;
        if (l.Upper)
        {
            above = l.Lim;
            if (Flatten(l.Base) is [Limit { Upper: false } inner])
            {
                below = inner.Lim;
                core = inner.Base;
            }
        }
        else
        {
            below = l.Lim;
        }

        // "=" stays \overset{a}{=}: \xlongequal would change its look on a round trip
        if (Flatten(core) is [Run { Text: var chr }] && chr != "=" && OverUnderCommands.TryGetArrowCommand(chr, out var arrow)
            && (above != null && !IsEmpty(above) || below != null && !IsEmpty(below)))
        {
            b.Command(arrow);
            if (below != null && !IsEmpty(below))
            {
                b.Text("[");
                WriteNode(below, b);
                b.Text("]");
            }
            WriteGroup(above ?? FormulaRow.Empty, b);
            return;
        }

        if (above != null && below != null)
        {
            b.Command("\\overunderset");
            WriteGroup(above, b);
            WriteGroup(below, b);
            WriteGroup(core, b);
        }
        else
        {
            WriteOverUnder(above != null ? "\\overset" : "\\underset", (above ?? below)!, core, b);
        }
    }

    private static void WriteOverUnder(string command, FormulaNode label, FormulaNode body, LatexBuilder b)
    {
        b.Command(command);
        WriteGroup(label, b);
        WriteGroup(body, b);
    }

    private static FormulaNode WriteText(string chr) => new Run(chr, new RunStyle());

    private static bool IsEmpty(FormulaNode node) => Flatten(node).Count == 0;
}
