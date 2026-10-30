using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary>
    /// <c>m:r</c>: the text of its <c>m:t</c> children with the style of <c>m:rPr</c> (<c>m:nor</c> wins over
    /// <c>m:sty</c>/<c>m:scr</c>). Other children (<c>w:rPr</c>, …) are ignored.
    /// In non-normal math runs, replaces U+2212 with '-', decodes math alphanumerics, and merges consecutive characters of equal style.
    /// </summary>
    private static void ReadRun(XElement r, List<FormulaNode> items, List<Diagnostic> d, bool inEqArrayRow = false)
    {
        var rPr = r.Element(M + "rPr");
        if (rPr?.Element(M + "brk") is not null)
        {
            d.Add(new Diagnostic("lossy", "manual break (m:brk) is dropped", -1));
        }

        if (inEqArrayRow && rPr?.Element(M + "aln") is { } aln && !IsOff(aln))
        {
            items.Add(new AlignMark());
        }

        var text = string.Concat(r.Elements(M + "t").Select(t => t.Value));
        if (text.Length == 0) return;
        if (rPr?.Element(M + "nor") is { } nor && !IsOff(nor))
        {
            items.Add(new Run(text, new RunStyle(Normal: true)));
            return;
        }

        Sty? explicitSty = null;
        Scr? explicitScr = null;

        if (rPr != null)
        {
            if (rPr.Element(M + "sty") is { } styEl)
            {
                switch ((string?)styEl.Attribute(M + "val"))
                {
                    case "p": explicitSty = Sty.Plain; break;
                    case "b": explicitSty = Sty.Bold; break;
                    case "bi": explicitSty = Sty.BoldItalic; break;
                    case "i": explicitSty = Sty.Italic; break;
                    case var v:
                        d.Add(new Diagnostic("unsupported-omml", $"unsupported m:sty value '{v}'; ignored", -1));
                        break;
                }
            }
            if (rPr.Element(M + "scr") is { } scrEl)
            {
                switch ((string?)scrEl.Attribute(M + "val"))
                {
                    case "roman": explicitScr = Scr.Roman; break;
                    case "script": explicitScr = Scr.Script; break;
                    case "fraktur": explicitScr = Scr.Fraktur; break;
                    case "double-struck": explicitScr = Scr.DoubleStruck; break;
                    case "sans-serif": explicitScr = Scr.SansSerif; break;
                    case "monospace": explicitScr = Scr.Monospace; break;
                    case var v:
                        d.Add(new Diagnostic("unsupported-omml", $"unsupported m:scr value '{v}'; ignored", -1));
                        break;
                }
            }
        }

        var baseStyle = new RunStyle(explicitSty ?? Sty.Italic, explicitScr ?? Scr.Roman);

        var decodedRunes = new List<(string Text, RunStyle Style, bool IsPrime)>();
        foreach (var rune in text.EnumerateRunes())
        {
            bool isPrime = rune.Value is 0x2032 or 0x2033 or 0x2034;
            if (isPrime)
            {
                decodedRunes.Add((rune.ToString(), baseStyle, true));
            }
            else if (rune.Value == '\u2212')
            {
                decodedRunes.Add(("-", baseStyle, false));
            }
            else if (MathAlphaDecoder.TryDecode(rune, out var ascii, out var decodedStyle))
            {
                decodedRunes.Add((ascii.ToString(), new RunStyle(explicitSty ?? decodedStyle.Sty, explicitScr ?? decodedStyle.Scr), false));
            }
            else
            {
                decodedRunes.Add((rune.ToString(), baseStyle, false));
            }
        }

        var runBuffer = new System.Text.StringBuilder();
        RunStyle? currentStyle = null;

        void Flush()
        {
            if (runBuffer.Length > 0 && currentStyle != null)
            {
                items.Add(new Run(runBuffer.ToString(), currentStyle));
                runBuffer.Clear();
                currentStyle = null;
            }
        }

        for (int i = 0; i < decodedRunes.Count; i++)
        {
            var cur = decodedRunes[i];
            if (!cur.IsPrime && i + 1 < decodedRunes.Count && decodedRunes[i + 1].IsPrime)
            {
                Flush();
                var primeBuffer = new System.Text.StringBuilder();
                int j = i + 1;
                while (j < decodedRunes.Count && decodedRunes[j].IsPrime)
                {
                    primeBuffer.Append(decodedRunes[j].Text);
                    j++;
                }
                var baseRun = new Run(cur.Text, cur.Style);
                var supRun = new Run(primeBuffer.ToString(), baseStyle);
                items.Add(new Script(baseRun, null, supRun));
                i = j - 1;
            }
            else
            {
                if (currentStyle != null && currentStyle != cur.Style)
                {
                    Flush();
                }
                currentStyle = cur.Style;
                runBuffer.Append(cur.Text);
            }
        }

        Flush();
    }

    /// <summary>An on/off element whose <c>m:val</c> says off (<c>0</c>, <c>false</c>, <c>off</c>).</summary>
    private static bool IsOff(XElement e) => (string?)e.Attribute(M + "val") is "0" or "false" or "off";
}
