using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary><c>m:acc</c>; without <c>m:chr</c> the accent is U+0302 (circumflex).</summary>
    private static FormulaNode ReadAccent(XElement el, List<Diagnostic> d)
    {
        var chr = (string?)el.Element(M + "accPr")?.Element(M + "chr")?.Attribute(M + "val") ?? "̂";
        return new Accent(chr, ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:bar</c>: <c>m:pos</c> <c>bot</c> is the underline, anything else the overline.</summary>
    private static FormulaNode ReadBar(XElement el, List<Diagnostic> d)
    {
        var pos = (string?)el.Element(M + "barPr")?.Element(M + "pos")?.Attribute(M + "val");
        return new Bar(pos != "bot", ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:groupChr</c>; the defaults are an underbrace (U+23DF) below the body.</summary>
    private static FormulaNode ReadGroupChar(XElement el, List<Diagnostic> d)
    {
        var pr = el.Element(M + "groupChrPr");
        var chr = (string?)pr?.Element(M + "chr")?.Attribute(M + "val") ?? "⏟";
        var pos = (string?)pr?.Element(M + "pos")?.Attribute(M + "val");
        return new GroupChar(chr, pos == "top", ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:limLow</c> / <c>m:limUpp</c> outside a function name.</summary>
    private static FormulaNode ReadLimit(XElement el, List<Diagnostic> d) =>
        new Limit(ReadContent(el.Element(M + "e"), d), ReadContent(el.Element(M + "lim"), d), el.Name.LocalName == "limUpp");
}
