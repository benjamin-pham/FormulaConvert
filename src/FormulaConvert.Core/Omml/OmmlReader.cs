using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Latex;

namespace FormulaConvert.Omml;

/// <summary>
/// Reads OMML into the AST, ignoring Word noise (<c>m:ctrlPr</c>, non-<c>m</c> elements, whitespace text).
/// Element kinds are dispatched in <see cref="ReadInto"/>; feature slices add a case there and implement it in
/// <c>OmmlReader.&lt;Group&gt;.cs</c>. Unknown <c>m:</c> elements produce an <c>unsupported-omml</c> warning and
/// their children are read in place.
/// </summary>
internal static partial class OmmlReader
{
    private static readonly XNamespace M = OmmlNames.M;

    /// <summary>Reads the content of an <c>m:oMath</c> element.</summary>
    public static FormulaNode Read(XElement root, List<Diagnostic> d) => ReadContent(root, d);

    /// <summary>Reads the children of a container element (<c>m:oMath</c>, <c>m:e</c>, <c>m:num</c>, …) as one row.</summary>
    internal static FormulaNode ReadContent(XElement? container, List<Diagnostic> d)
    {
        var items = new List<FormulaNode>();
        if (container != null) ReadInto(container, items, d);
        return LatexParser.Rowify(items);
    }

    private static void ReadInto(XElement container, List<FormulaNode> items, List<Diagnostic> d)
    {
        foreach (var el in container.Elements())
        {
            if (el.Name.Namespace != M) continue;
            switch (el.Name.LocalName)
            {
                case "ctrlPr":
                    break;
                case "r":
                    if (ReadRun(el) is { } run) items.Add(run);
                    break;
                // Feature slices add cases here, e.g. `case "f": items.Add(ReadFrac(el, d)); break;`
                default:
                    d.Add(new Diagnostic("unsupported-omml", $"unsupported OMML element m:{el.Name.LocalName}; reading its content", -1));
                    ReadInto(el, items, d);
                    break;
            }
        }
    }

    private static Run? ReadRun(XElement r)
    {
        // m:rPr (sty/scr/nor) is read by the style slice; everything else in m:r except m:t is ignored.
        var text = string.Concat(r.Elements(M + "t").Select(t => t.Value));
        return text.Length == 0 ? null : new Run(text, new RunStyle());
    }
}
