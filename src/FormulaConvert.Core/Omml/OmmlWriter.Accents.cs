using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    private static XElement WriteAccent(Accent a) =>
        new(M + "acc", new XElement(M + "accPr", new XElement(M + "chr", new XAttribute(M + "val", a.Chr))), Element("e", a.Body));

    /// <summary><c>m:bar</c>; <c>m:barPr</c> only for the bottom bar (top is the default).</summary>
    private static XElement WriteBar(Bar b)
    {
        var e = new XElement(M + "bar");
        if (!b.Top)
            e.Add(new XElement(M + "barPr", new XElement(M + "pos", new XAttribute(M + "val", "bot"))));
        e.Add(Element("e", b.Body));
        return e;
    }

    private static XElement WriteGroupChar(GroupChar g) =>
        new(M + "groupChr",
            new XElement(M + "groupChrPr",
                new XElement(M + "chr", new XAttribute(M + "val", g.Chr)),
                new XElement(M + "pos", new XAttribute(M + "val", g.Top ? "top" : "bot"))),
            Element("e", g.Body));
}
