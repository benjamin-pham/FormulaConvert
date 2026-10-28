using FormulaConvert.Ast;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary>
    /// <c>\begin{pmatrix}a&amp;b\\c&amp;d\end{pmatrix}</c>; <c>array</c> also writes its column specification
    /// (one letter per column, padded with <c>c</c> to the widest row).
    /// </summary>
    private static void WriteMatrix(string name, Matrix m, LatexBuilder b)
    {
        b.Text("\\begin{" + name + "}");
        if (name == "array")
        {
            int width = Math.Max(m.ColumnAlign?.Length ?? 0, m.Rows.Count == 0 ? 0 : m.Rows.Max(r => r.Count));
            b.Text("{" + (m.ColumnAlign ?? "").PadRight(width, 'c') + "}");
        }
        for (int i = 0; i < m.Rows.Count; i++)
        {
            if (i > 0) b.Text("\\\\");
            for (int j = 0; j < m.Rows[i].Count; j++)
            {
                if (j > 0) b.Text("&");
                WriteNode(m.Rows[i][j], b);
            }
        }
        b.Text("\\end{" + name + "}");
    }

    /// <summary>An equation array environment; a <see cref="Run"/> <c>&amp;</c> directly in a row is the alignment mark, written raw.</summary>
    private static void WriteEquations(string name, EqArray a, LatexBuilder b)
    {
        b.Text("\\begin{" + name + "}");
        for (int i = 0; i < a.Rows.Count; i++)
        {
            if (i > 0) b.Text("\\\\");
            var items = new List<FormulaNode>();
            Flatten(a.Rows[i], items);
            foreach (var item in items)
            {
                if (item is not Run run || !run.Text.Contains('&'))
                {
                    WriteNode(item, b);
                    continue;
                }
                var pieces = run.Text.Split('&');
                for (int k = 0; k < pieces.Length; k++)
                {
                    if (k > 0) b.Text("&");
                    if (pieces[k].Length > 0) WriteStyledRuns([run with { Text = pieces[k] }], b);
                }
            }
        }
        b.Text("\\end{" + name + "}");
    }

    private static bool HasAlignmentMark(EqArray a)
    {
        var items = new List<FormulaNode>();
        foreach (var row in a.Rows) Flatten(row, items);
        return items.Any(i => i is Run r && r.Text.Contains('&'));
    }

    private static void Flatten(FormulaNode node, List<FormulaNode> items)
    {
        if (node is FormulaRow row)
            foreach (var item in row.Items) Flatten(item, items);
        else
            items.Add(node);
    }
}
