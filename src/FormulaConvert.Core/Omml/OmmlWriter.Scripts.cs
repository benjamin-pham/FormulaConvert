using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary><c>m:sSub</c>, <c>m:sSup</c> or <c>m:sSubSup</c> depending on which scripts are present.</summary>
    private static XElement WriteScript(Script s)
    {
        var name = (s.Sub, s.Sup) switch
        {
            (not null, not null) => "sSubSup",
            (not null, null) => "sSub",
            _ => "sSup",
        };
        var e = new XElement(M + name, Element("e", s.Base));
        if (s.Sub != null) e.Add(Element("sub", s.Sub));
        if (s.Sup != null || s.Sub == null) e.Add(Element("sup", s.Sup ?? FormulaRow.Empty));
        return e;
    }

    /// <summary><c>m:sPre</c>; a missing script is written as an empty element (the schema requires both).</summary>
    private static XElement WritePreScript(PreScript s) =>
        new(M + "sPre", Element("sub", s.Sub ?? FormulaRow.Empty), Element("sup", s.Sup ?? FormulaRow.Empty), Element("e", s.Base));

    /// <summary><c>m:rad</c>; a missing degree is written as <c>m:degHide</c> with an empty <c>m:deg</c>.</summary>
    private static XElement WriteRadical(Radical r)
    {
        var e = new XElement(M + "rad");
        if (r.Degree == null)
            e.Add(new XElement(M + "radPr", new XElement(M + "degHide", new XAttribute(M + "val", "1"))));
        e.Add(Element("deg", r.Degree ?? FormulaRow.Empty), Element("e", r.Body));
        return e;
    }
}
