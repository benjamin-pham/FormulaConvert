using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    private static XElement WriteAccent(Accent a) =>
        new(M + "acc", new XElement(M + "accPr", new XElement(M + "chr", new XAttribute(M + "val", a.Chr))), Element("e", a.Body));

    /// <summary><c>m:bar</c> with an explicit <c>m:pos</c> (ECMA-376 defaults a missing one to <c>bot</c>; Word writes <c>top</c> for overbars).</summary>
    private static XElement WriteBar(Bar b) =>
        new(M + "bar",
            new XElement(M + "barPr", new XElement(M + "pos", new XAttribute(M + "val", b.Top ? "top" : "bot"))),
            Element("e", b.Body));

    private static XElement WriteGroupChar(GroupChar g) =>
        new(M + "groupChr",
            new XElement(M + "groupChrPr",
                new XElement(M + "chr", new XAttribute(M + "val", g.Chr)),
                new XElement(M + "pos", new XAttribute(M + "val", g.Top ? "top" : "bot"))),
            Element("e", g.Body));
}
