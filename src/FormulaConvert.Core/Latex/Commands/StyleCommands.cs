using System.Text;
using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>Font styles (<c>\mathbf</c>, <c>\mathbb</c>, …) and normal text (<c>\text</c>, <c>\mbox</c>, …).</summary>
internal static class StyleCommands
{
    /// <summary>Style commands in canonical writing order: command, <see cref="Sty"/> it sets (when not Italic) or <see cref="Scr"/> it sets.</summary>
    internal static readonly (string Command, Sty Sty)[] StyCommands =
    [
        ("\\mathrm", Sty.Plain),
        ("\\mathbf", Sty.Bold),
        ("\\mathbfit", Sty.BoldItalic),
    ];

    internal static readonly (string Command, Scr Scr)[] ScrCommands =
    [
        ("\\mathscr", Scr.Script),
        ("\\mathfrak", Scr.Fraktur),
        ("\\mathbb", Scr.DoubleStruck),
        ("\\mathsf", Scr.SansSerif),
        ("\\mathtt", Scr.Monospace),
    ];

    public static void Register(CommandTable t)
    {
        foreach (var name in new[] { "\\mathbf", "\\boldsymbol", "\\bm", "\\bold", "\\textbf" })
            AddSty(t, name, Sty.Bold);
        AddSty(t, "\\mathrm", Sty.Plain);
        AddSty(t, "\\mathbfit", Sty.BoldItalic);
        foreach (var name in new[] { "\\mathit", "\\textit" })
            t.Add(name, (p, n) => p.ReadArgument()); // the default style
        AddScr(t, "\\mathbb", Scr.DoubleStruck);
        AddScr(t, "\\Bbb", Scr.DoubleStruck);
        AddScr(t, "\\mathcal", Scr.Script);
        AddScr(t, "\\mathscr", Scr.Script);
        AddScr(t, "\\mathfrak", Scr.Fraktur);
        AddScr(t, "\\mathsf", Scr.SansSerif);
        AddScr(t, "\\mathtt", Scr.Monospace);
        AddScr(t, "\\texttt", Scr.Monospace);
        foreach (var name in new[] { "\\text", "\\textrm", "\\textnormal", "\\mbox" })
            t.Add(name, (p, n) => ReadText(p));
    }

    // The outer command only sets what the inner ones left at the default, so the innermost style wins.
    private static void AddSty(CommandTable t, string name, Sty sty) =>
        t.Add(name, (p, n) => Apply(p.ReadArgument(), s => s.Sty == Sty.Italic ? s with { Sty = sty } : s));

    private static void AddScr(CommandTable t, string name, Scr scr) =>
        t.Add(name, (p, n) => Apply(p.ReadArgument(), s => s.Scr == Scr.Roman ? s with { Scr = scr } : s));

    /// <summary><c>\text{…}</c>: the raw text as one normal-text run; <c>\%</c> etc. are unescaped.</summary>
    private static FormulaNode ReadText(LatexParser p)
    {
        int at = p.Position;
        var raw = p.ReadRawGroup();
        var text = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\' || i + 1 == raw.Length)
            {
                text.Append(raw[i]);
                continue;
            }
            char next = raw[i + 1];
            if ("%&$#_{}".Contains(next))
            {
                text.Append(next);
                i++;
            }
            else if (next == ' ')
            {
                text.Append(' ');
                i++;
            }
            else if (char.IsAsciiLetter(next))
            {
                int end = i + 1;
                while (end < raw.Length && char.IsAsciiLetter(raw[end])) end++;
                var command = raw[i..end];
                if (command is "\\backslash" or "\\textbackslash")
                {
                    text.Append('\\');
                    // a following {} or the space ending the command is not part of the text
                    if (end + 1 < raw.Length && raw[end] == '{' && raw[end + 1] == '}') end += 2;
                    else if (end < raw.Length && raw[end] == ' ') end++;
                }
                else
                {
                    p.Warn("lossy", $"{command} inside \\text is kept as literal text", at);
                    text.Append(command);
                }
                i = end - 1;
            }
            else
            {
                p.Warn("lossy", $"\\{next} inside \\text is kept as literal text", at);
                text.Append('\\').Append(next);
                i++;
            }
        }
        return text.Length == 0 ? FormulaRow.Empty : new Run(text.ToString(), new RunStyle(Normal: true));
    }

    /// <summary>Applies <paramref name="change"/> to the style of every non-normal <see cref="Run"/> inside <paramref name="body"/>.</summary>
    internal static FormulaNode Apply(FormulaNode body, Func<RunStyle, RunStyle> change)
    {
        FormulaNode A(FormulaNode n) => Apply(n, change);
        FormulaNode? O(FormulaNode? n) => n == null ? null : Apply(n, change);
        return body switch
        {
            Run { Style.Normal: true } => body,
            Run r => r with { Style = change(r.Style) },
            FormulaRow row => new FormulaRow(row.Items.Select(A).ToList()),
            Frac f => f with { Num = A(f.Num), Den = A(f.Den) },
            Radical r => r with { Degree = O(r.Degree), Body = A(r.Body) },
            Script s => s with { Base = A(s.Base), Sub = O(s.Sub), Sup = O(s.Sup) },
            PreScript s => s with { Base = A(s.Base), Sub = O(s.Sub), Sup = O(s.Sup) },
            Nary n => n with { Sub = O(n.Sub), Sup = O(n.Sup), Body = A(n.Body) },
            Delimited d => d with { Parts = d.Parts.Select(A).ToList() },
            Matrix m => m with { Rows = m.Rows.Select(row => (IReadOnlyList<FormulaNode>)row.Select(A).ToList()).ToList() },
            EqArray e => e with { Rows = e.Rows.Select(A).ToList() },
            Accent a => a with { Body = A(a.Body) },
            Bar b => b with { Body = A(b.Body) },
            GroupChar g => g with { Body = A(g.Body) },
            Limit l => l with { Base = A(l.Base), Lim = A(l.Lim) },
            FunctionApply f => f with { Name = A(f.Name), Arg = A(f.Arg) },
            BorderBox b => b with { Body = A(b.Body) },
            Phantom ph => ph with { Body = A(ph.Body) },
            _ => body,
        };
    }
}
