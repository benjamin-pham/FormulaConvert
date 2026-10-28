using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary><c>m:m</c>; <c>m:mPr</c> lists one <c>m:mc</c> per column up to the last non-centered one, and only when there is one.</summary>
    private static XElement WriteMatrix(Matrix m)
    {
        var e = new XElement(M + "m");
        if (m.ColumnAlign is { Length: > 0 } align)
        {
            var mcs = new XElement(M + "mcs");
            foreach (var c in align)
            {
                var jc = c switch { 'l' => "left", 'r' => "right", _ => "center" };
                mcs.Add(new XElement(M + "mc", new XElement(M + "mcPr",
                    new XElement(M + "count", new XAttribute(M + "val", 1)),
                    new XElement(M + "mcJc", new XAttribute(M + "val", jc)))));
            }
            e.Add(new XElement(M + "mPr", mcs));
        }
        foreach (var row in m.Rows)
        {
            var mr = new XElement(M + "mr");
            foreach (var cell in row) mr.Add(Element("e", cell));
            if (!mr.HasElements) mr.Value = "";
            e.Add(mr);
        }
        if (!e.HasElements) e.Value = "";
        return e;
    }

    private static XElement WriteEqArray(EqArray a)
    {
        var e = new XElement(M + "eqArr");
        foreach (var row in a.Rows) e.Add(Element("e", row));
        if (!e.HasElements) e.Value = "";
        return e;
    }
}
