using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    private static FormulaNode ReadFrac(XElement f, List<Diagnostic> d)
    {
        var type = (string?)f.Element(M + "fPr")?.Element(M + "type")?.Attribute(M + "val") ?? "bar";
        var kind = type switch
        {
            "noBar" => FracKind.NoBar,
            "lin" => FracKind.Lin,
            "skw" => FracKind.Skw,
            _ => FracKind.Bar,
        };
        if (type == "lin")
            d.Add(new Diagnostic("lossy", "linear fraction (m:type lin) is read as a/b", -1));
        return new Frac(ReadContent(f.Element(M + "num"), d), ReadContent(f.Element(M + "den"), d), kind);
    }
}
