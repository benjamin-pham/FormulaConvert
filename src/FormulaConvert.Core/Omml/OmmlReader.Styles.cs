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
    private static void ReadRun(XElement r, List<FormulaNode> items, List<Diagnostic> d)
    {
        var text = string.Concat(r.Elements(M + "t").Select(t => t.Value));
        if (text.Length == 0) return;
        var rPr = r.Element(M + "rPr");
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
        var runBuffer = new System.Text.StringBuilder();
        RunStyle? currentStyle = null;

        void Flush()
        {
            if (runBuffer.Length > 0 && currentStyle != null)
            {
                items.Add(new Run(runBuffer.ToString(), currentStyle));
                runBuffer.Clear();
            }
        }

        foreach (var rune in text.EnumerateRunes())
        {
            string chText;
            RunStyle chStyle;

            if (rune.Value == '\u2212')
            {
                chText = "-";
                chStyle = baseStyle;
            }
            else if (MathAlphaDecoder.TryDecode(rune, out var ascii, out var decodedStyle))
            {
                chText = ascii.ToString();
                chStyle = new RunStyle(explicitSty ?? decodedStyle.Sty, explicitScr ?? decodedStyle.Scr);
            }
            else
            {
                chText = rune.ToString();
                chStyle = baseStyle;
            }

            if (currentStyle != null && currentStyle != chStyle)
            {
                Flush();
            }

            currentStyle = chStyle;
            runBuffer.Append(chText);
        }

        Flush();
    }

    /// <summary>An on/off element whose <c>m:val</c> says off (<c>0</c>, <c>false</c>, <c>off</c>).</summary>
    private static bool IsOff(XElement e) => (string?)e.Attribute(M + "val") is "0" or "false" or "off";
}
