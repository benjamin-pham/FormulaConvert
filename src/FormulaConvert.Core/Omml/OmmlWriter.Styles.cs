using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    private static XElement WriteRun(string text, RunStyle style)
    {
        var r = new XElement(M + "r");
        var rPr = WriteRunProperties(style);
        if (rPr != null) r.Add(rPr);
        var t = new XElement(M + "t", text);
        bool edgeSpace = text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]));
        if (edgeSpace || style.Normal && text.Any(char.IsWhiteSpace))
            t.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
        r.Add(t);
        return r;
    }

    /// <summary>
    /// <c>m:rPr</c> for a style, or null for the default style. Children follow the schema order: <c>m:nor</c>
    /// (which replaces the other styles), else <c>m:scr</c> then <c>m:sty</c>.
    /// </summary>
    private static XElement? WriteRunProperties(RunStyle style)
    {
        var rPr = new XElement(M + "rPr");
        if (style.Normal)
        {
            rPr.Add(new XElement(M + "nor"));
            return rPr;
        }
        if (style.Scr != Scr.Roman)
            rPr.Add(Val("scr", style.Scr switch
            {
                Scr.Script => "script",
                Scr.Fraktur => "fraktur",
                Scr.DoubleStruck => "double-struck",
                Scr.SansSerif => "sans-serif",
                _ => "monospace",
            }));
        if (style.Sty != Sty.Italic)
            rPr.Add(Val("sty", style.Sty switch { Sty.Plain => "p", Sty.Bold => "b", _ => "bi" }));
        return rPr.HasElements ? rPr : null;
    }

    private static XElement Val(string name, string value) => new(M + name, new XAttribute(M + "val", value));
}
