using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static class OmmlNames
{
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
}

/// <summary>
/// Writes the AST as minimal OMML. Node kinds are dispatched in <see cref="WriteNode"/>; feature slices add a
/// case there and implement it in <c>OmmlWriter.&lt;Group&gt;.cs</c>.
/// </summary>
internal static partial class OmmlWriter
{
    private static readonly XNamespace M = OmmlNames.M;

    /// <summary>An <c>m:oMath</c> element (declaring the <c>m</c> prefix) holding the content of <paramref name="root"/>.</summary>
    public static XElement Write(FormulaNode root)
    {
        var e = new XElement(M + "oMath", new XAttribute(XNamespace.Xmlns + "m", M.NamespaceName));
        WriteContent(e, root);
        if (!e.HasElements) e.Value = ""; // serialize as <m:oMath …></m:oMath>
        return e;
    }

    /// <summary>An element <c>m:{localName}</c> holding the content of <paramref name="content"/> (e.g. <c>m:e</c>, <c>m:num</c>).</summary>
    internal static XElement Element(string localName, FormulaNode content)
    {
        var e = new XElement(M + localName);
        WriteContent(e, content);
        if (!e.HasElements) e.Value = ""; // serialize as <m:num></m:num>, not <m:num />
        return e;
    }

    /// <summary>Appends the content of a node to <paramref name="parent"/>: rows are flattened and adjacent runs of the same style merged.</summary>
    internal static void WriteContent(XElement parent, FormulaNode node)
    {
        var items = new List<FormulaNode>();
        Flatten(node, items);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is Run run)
            {
                var text = new System.Text.StringBuilder(run.Text);
                while (i + 1 < items.Count && items[i + 1] is Run next && next.Style == run.Style)
                {
                    text.Append(next.Text);
                    i++;
                }
                parent.Add(WriteRun(text.ToString(), run.Style));
            }
            else
            {
                parent.Add(WriteNode(items[i]));
            }
        }
    }

    private static void Flatten(FormulaNode node, List<FormulaNode> items)
    {
        if (node is FormulaRow row)
            foreach (var item in row.Items) Flatten(item, items);
        else
            items.Add(node);
    }

    private static XElement WriteNode(FormulaNode node) => node switch
    {
        Frac f => WriteFrac(f),
        Delimited { Parts: [Frac { Kind: FracKind.NoBar }] } d when d is { Open: "(", Close: ")" } => WriteBinomial(d),
        Script s => WriteScript(s),
        PreScript s => WritePreScript(s),
        Radical r => WriteRadical(r),
        // Feature slices add cases here, e.g. `Frac f => WriteFrac(f),`
        _ => throw new NotSupportedException($"OmmlWriter does not support {node.GetType().Name} nodes."),
    };

    private static XElement WriteRun(string text, RunStyle style)
    {
        var r = new XElement(M + "r");
        var rPr = WriteRunProperties(style);
        if (rPr != null) r.Add(rPr);
        var t = new XElement(M + "t", text);
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
            t.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
        r.Add(t);
        return r;
    }

    /// <summary><c>m:rPr</c> for a style, or null for the default style. Non-default styles are added by the style slice.</summary>
    private static XElement? WriteRunProperties(RunStyle style) =>
        style == new RunStyle()
            ? null
            : throw new NotSupportedException($"OmmlWriter does not support run style {style}.");
}
