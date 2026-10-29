using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Latex.Commands;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary><c>m:acc</c>; without <c>m:chr</c> the accent is U+0302 (circumflex).</summary>
    private static FormulaNode ReadAccent(XElement el, List<Diagnostic> d)
    {
        var chr = (string?)el.Element(M + "accPr")?.Element(M + "chr")?.Attribute(M + "val") ?? "̂";
        if (chr == "\u033F")
            return new Bar(true, new Bar(true, ReadContent(el.Element(M + "e"), d)));
        if (chr == "\u0305")
            return new Bar(true, ReadContent(el.Element(M + "e"), d));
        if (chr == "\u0332")
            return new Bar(false, ReadContent(el.Element(M + "e"), d));
        return new Accent(chr, ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:bar</c>: <c>m:pos</c> <c>top</c> is the overline; <c>bot</c> or no <c>m:pos</c> (the ECMA-376 default) the underline.</summary>
    private static FormulaNode ReadBar(XElement el, List<Diagnostic> d)
    {
        var pos = (string?)el.Element(M + "barPr")?.Element(M + "pos")?.Attribute(M + "val");
        return new Bar(pos == "top", ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:groupChr</c>; the defaults are an underbrace (U+23DF) below the body.</summary>
    private static FormulaNode ReadGroupChar(XElement el, List<Diagnostic> d)
    {
        var pr = el.Element(M + "groupChrPr");
        var chr = (string?)pr?.Element(M + "chr")?.Attribute(M + "val") ?? "⏟";
        var pos = (string?)pr?.Element(M + "pos")?.Attribute(M + "val");
        var vertJc = (string?)pr?.Element(M + "vertJc")?.Attribute(M + "val");
        bool top = pos == "top";
        bool vertJcTop = (!top && vertJc == "top") || (top && vertJc == "bot" && OverUnderCommands.TryGetArrowCommand(chr, out _) && chr != "=");
        return new GroupChar(chr, top, ReadContent(el.Element(M + "e"), d), vertJcTop);
    }

    /// <summary><c>m:limLow</c> / <c>m:limUpp</c> outside a function name.</summary>
    private static FormulaNode ReadLimit(XElement el, List<Diagnostic> d) =>
        new Limit(ReadContent(el.Element(M + "e"), d), ReadContent(el.Element(M + "lim"), d), el.Name.LocalName == "limUpp");
}
