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
        foreach (var row in a.Rows)
        {
            var rowElement = new XElement(M + "e");
            WriteEqArrayRow(rowElement, row);
            if (!rowElement.HasElements) rowElement.Value = "";
            e.Add(rowElement);
        }
        if (a.Rows.Count == 0) e.Add(Element("e", FormulaRow.Empty)); // OMML has no empty m:eqArr
        return e;
    }

    /// <summary>
    /// One <c>m:e</c> of an equation array. An <see cref="AlignMark"/> is <c>m:aln</c> on the run that follows it
    /// (ECMA-376 §22.1.2.3); when no run follows (end of row, another alignment point, or a non-run item) it is an empty run.
    /// </summary>
    private static void WriteEqArrayRow(XElement parent, FormulaNode row)
    {
        NestingGuard.EnsureStack(-1);
        var items = new List<FormulaNode>();
        Flatten(row, items);
        if (items is [Placeholder]) return;
        bool aln = false;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is AlignMark)
            {
                if (aln) parent.Add(WriteRun("", new RunStyle(), hasAln: true));
                aln = true;
            }
            else if (items[i] is Run run)
            {
                var text = new System.Text.StringBuilder(run.Text);
                while (i + 1 < items.Count && items[i + 1] is Run next && next.Style == run.Style)
                {
                    text.Append(next.Text);
                    i++;
                }
                parent.Add(WriteRun(text.ToString(), run.Style, aln));
                aln = false;
            }
            else
            {
                if (aln) parent.Add(WriteRun("", new RunStyle(), hasAln: true));
                aln = false;
                parent.Add(WriteNode(items[i]));
            }
        }
        if (aln) parent.Add(WriteRun("", new RunStyle(), hasAln: true));
    }
}
