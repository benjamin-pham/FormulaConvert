using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary><c>m:m</c> (never empty); <c>m:mPr</c> lists one <c>m:mc</c> per column up to the last non-centered one, and only when there is one.</summary>
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
        // OMML has no empty m:m or m:mr: pad every row to the widest (at least one cell) and give an empty matrix one row.
        int width = Math.Max(1, m.Rows.Count == 0 ? 0 : m.Rows.Max(r => r.Count));
        foreach (var row in m.Rows.Count == 0 ? [[]] : m.Rows)
        {
            var mr = new XElement(M + "mr");
            foreach (var cell in row) mr.Add(Element("e", cell));
            for (int k = row.Count; k < width; k++) mr.Add(Element("e", FormulaRow.Empty));
            e.Add(mr);
        }
        return e;
    }

    private static XElement WriteEqArray(EqArray a)
    {
        var e = new XElement(M + "eqArr");
        foreach (var row in a.Rows) e.Add(Element("e", row));
        if (a.Rows.Count == 0) e.Add(Element("e", FormulaRow.Empty)); // OMML has no empty m:eqArr
        return e;
    }
}
