using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    private const int MaxColumns = 1000;

    /// <summary><c>m:m</c>: rows of <c>m:e</c> cells; column alignment from <c>m:mPr/m:mcs</c> (<c>m:mcJc</c> left/right, anything else centered).</summary>
    private static FormulaNode ReadMatrix(XElement el, List<Diagnostic> d)
    {
        var rows = el.Elements(M + "mr")
            .Select(mr => (IReadOnlyList<FormulaNode>)mr.Elements(M + "e").Select(e => ReadContent(e, d)).ToList())
            .ToList();
        var align = new System.Text.StringBuilder();
        foreach (var mc in el.Element(M + "mPr")?.Element(M + "mcs")?.Elements(M + "mc") ?? [])
        {
            var pr = mc.Element(M + "mcPr");
            int count = int.TryParse((string?)pr?.Element(M + "count")?.Attribute(M + "val"), out var n) ? Math.Clamp(n, 1, MaxColumns) : 1;
            char jc = (string?)pr?.Element(M + "mcJc")?.Attribute(M + "val") switch { "left" => 'l', "right" => 'r', _ => 'c' };
            align.Append(jc, Math.Min(count, MaxColumns - align.Length));
        }
        var columns = align.ToString().TrimEnd('c');
        return new Matrix(rows, columns.Length == 0 ? null : columns);
    }

    /// <summary><c>m:eqArr</c>: one row per <c>m:e</c>.</summary>
    private static FormulaNode ReadEqArray(XElement el, List<Diagnostic> d) =>
        new EqArray(el.Elements(M + "e").Select(e => ReadContent(e, d)).ToList());
}
