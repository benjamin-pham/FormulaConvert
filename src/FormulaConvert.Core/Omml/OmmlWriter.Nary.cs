using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary><c>m:nary</c>; <c>m:limLoc</c> only when it differs from the character's default, <c>m:subHide</c>/<c>m:supHide</c> for absent scripts.</summary>
    private static XElement WriteNary(Nary n)
    {
        bool defaultSubSup = NaryTable.TryGetByChar(n.Chr, out var info) && info.SubSupDefault;
        var pr = new XElement(M + "naryPr", new XElement(M + "chr", new XAttribute(M + "val", n.Chr)));
        if (n.SubSupLimits != defaultSubSup)
            pr.Add(new XElement(M + "limLoc", new XAttribute(M + "val", n.SubSupLimits ? "subSup" : "undOvr")));
        if (n.Sub == null) pr.Add(new XElement(M + "subHide", new XAttribute(M + "val", "1")));
        if (n.Sup == null) pr.Add(new XElement(M + "supHide", new XAttribute(M + "val", "1")));
        return new XElement(M + "nary", pr,
            Element("sub", n.Sub ?? FormulaRow.Empty), Element("sup", n.Sup ?? FormulaRow.Empty), Element("e", n.Body));
    }

    private static XElement WriteFunctionApply(FunctionApply f) =>
        new(M + "func", Element("fName", f.Name), Element("e", f.Arg));

    /// <summary><c>m:limLow</c> (limit below the base) or <c>m:limUpp</c>.</summary>
    private static XElement WriteLimit(Limit l) =>
        new(M + (l.Upper ? "limUpp" : "limLow"), Element("e", l.Base), Element("lim", l.Lim));
}
