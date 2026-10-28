using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary><c>m:f</c>; the <c>m:fPr</c> is omitted for the default bar fraction.</summary>
    private static XElement WriteFrac(Frac f)
    {
        var e = new XElement(M + "f");
        if (f.Kind != FracKind.Bar)
        {
            var type = f.Kind == FracKind.NoBar ? "noBar" : "lin";
            e.Add(new XElement(M + "fPr", new XElement(M + "type", new XAttribute(M + "val", type))));
        }
        e.Add(Element("num", f.Num), Element("den", f.Den));
        return e;
    }

    /// <summary>A binomial: <c>m:d</c> with default parentheses around one bar-less fraction.</summary>
    private static XElement WriteBinomial(Delimited d) =>
        new(M + "d", Element("e", d.Parts[0]));
}
