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
            _ => FracKind.Bar,
        };
        if (type == "skw")
            d.Add(new Diagnostic("lossy", "skewed fraction (m:type skw) is read as a regular fraction", -1));
        return new Frac(ReadContent(f.Element(M + "num"), d), ReadContent(f.Element(M + "den"), d), kind);
    }

    /// <summary>True for an <c>m:d</c> with default parentheses holding exactly one bar-less <c>m:f</c> (a binomial).</summary>
    private static bool IsBinomial(XElement d)
    {
        var props = d.Element(M + "dPr");
        if (props != null)
        {
            if (!IsDefault(props.Element(M + "begChr"), "(") || !IsDefault(props.Element(M + "endChr"), ")")) return false;
            if (props.Element(M + "grow") != null) return false;
        }
        var parts = d.Elements(M + "e").ToList();
        if (parts.Count != 1) return false;
        var content = parts[0].Elements().Where(e => e.Name != M + "ctrlPr").ToList();
        return content.Count == 1
            && content[0].Name == M + "f"
            && (string?)content[0].Element(M + "fPr")?.Element(M + "type")?.Attribute(M + "val") == "noBar";
    }

    private static bool IsDefault(XElement? chr, string value) =>
        chr == null || ((string?)chr.Attribute(M + "val") ?? value) == value;

    private static FormulaNode ReadBinomial(XElement d, List<Diagnostic> d2) =>
        FormulaConvert.Latex.Commands.FractionCommands.Binomial(
            ReadContent(d.Element(M + "e")?.Element(M + "f")?.Element(M + "num"), d2),
            ReadContent(d.Element(M + "e")?.Element(M + "f")?.Element(M + "den"), d2));
}
