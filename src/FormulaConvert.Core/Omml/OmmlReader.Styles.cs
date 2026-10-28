using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary>
    /// <c>m:r</c>: the text of its <c>m:t</c> children with the style of <c>m:rPr</c> (<c>m:nor</c> wins over
    /// <c>m:sty</c>/<c>m:scr</c>). Other children (<c>w:rPr</c>, …) are ignored.
    /// </summary>
    private static Run? ReadRun(XElement r, List<Diagnostic> d)
    {
        var text = string.Concat(r.Elements(M + "t").Select(t => t.Value));
        if (text.Length == 0) return null;
        var rPr = r.Element(M + "rPr");
        if (rPr == null) return new Run(text, new RunStyle());
        if (rPr.Element(M + "nor") is { } nor && !IsOff(nor))
            return new Run(text, new RunStyle(Normal: true));

        var sty = Sty.Italic;
        var scr = Scr.Roman;
        if (rPr.Element(M + "sty") is { } styEl)
        {
            switch ((string?)styEl.Attribute(M + "val"))
            {
                case "p": sty = Sty.Plain; break;
                case "b": sty = Sty.Bold; break;
                case "bi": sty = Sty.BoldItalic; break;
                case "i": break;
                case var v:
                    d.Add(new Diagnostic("unsupported-omml", $"unsupported m:sty value '{v}'; ignored", -1));
                    break;
            }
        }
        if (rPr.Element(M + "scr") is { } scrEl)
        {
            switch ((string?)scrEl.Attribute(M + "val"))
            {
                case "roman": break;
                case "script": scr = Scr.Script; break;
                case "fraktur": scr = Scr.Fraktur; break;
                case "double-struck": scr = Scr.DoubleStruck; break;
                case "sans-serif": scr = Scr.SansSerif; break;
                case "monospace": scr = Scr.Monospace; break;
                case var v:
                    d.Add(new Diagnostic("unsupported-omml", $"unsupported m:scr value '{v}'; ignored", -1));
                    break;
            }
        }
        return new Run(text, new RunStyle(sty, scr));
    }

    /// <summary>An on/off element whose <c>m:val</c> says off (<c>0</c>, <c>false</c>, <c>off</c>).</summary>
    private static bool IsOff(XElement e) => (string?)e.Attribute(M + "val") is "0" or "false" or "off";
}
