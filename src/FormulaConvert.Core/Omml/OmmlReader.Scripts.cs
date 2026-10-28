using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    private static FormulaNode ReadSubSup(XElement el, bool sub, bool sup, List<Diagnostic> d) =>
        new Script(
            ReadContent(el.Element(M + "e"), d),
            sub ? ReadContent(el.Element(M + "sub"), d) : null,
            sup ? ReadContent(el.Element(M + "sup"), d) : null);

    /// <summary><c>m:sPre</c>: an empty sub or sup is "absent" (Word writes empty placeholders).</summary>
    private static FormulaNode ReadPreScript(XElement el, List<Diagnostic> d) =>
        new PreScript(
            ReadContent(el.Element(M + "e"), d),
            NonEmpty(ReadContent(el.Element(M + "sub"), d)),
            NonEmpty(ReadContent(el.Element(M + "sup"), d)));

    private static FormulaNode? NonEmpty(FormulaNode node) => node is FormulaRow { Items.Count: 0 } ? null : node;

    /// <summary><c>m:rad</c>; the degree is absent when <c>m:degHide</c> is on (a missing <c>m:val</c> means on) or <c>m:deg</c> is missing.</summary>
    private static FormulaNode ReadRadical(XElement el, List<Diagnostic> d)
    {
        var hide = el.Element(M + "radPr")?.Element(M + "degHide");
        bool hidden = hide != null && ((string?)hide.Attribute(M + "val") ?? "1") is "1" or "true" or "on";
        var deg = el.Element(M + "deg");
        var degree = hidden || deg == null ? null : ReadContent(deg, d);
        return new Radical(degree, ReadContent(el.Element(M + "e"), d));
    }
}
