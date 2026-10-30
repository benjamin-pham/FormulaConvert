using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Latex;

namespace FormulaConvert.Omml;

/// <summary>
/// Reads OMML into the AST, ignoring Word noise (<c>m:ctrlPr</c>, non-<c>m</c> elements, whitespace text) but reading
/// through Word wrappers such as tracked insertions (see <see cref="ReadForeign"/>).
/// Element kinds are dispatched in <see cref="ReadInto"/>; feature slices add a case there and implement it in
/// <c>OmmlReader.&lt;Group&gt;.cs</c>. Unknown <c>m:</c> elements produce an <c>unsupported-omml</c> warning and
/// their children are read in place.
/// </summary>
internal static partial class OmmlReader
{
    private static readonly XNamespace M = OmmlNames.M;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>WordprocessingML wrappers whose math content is current: tracked insertions, smart tags, content controls.</summary>
    private static readonly HashSet<string> TransparentW = ["ins", "moveTo", "smartTag", "customXml", "sdt", "sdtContent"];

    /// <summary>WordprocessingML wrappers whose content is deleted (tracked deletions and move sources).</summary>
    private static readonly HashSet<string> DeletedW = ["del", "moveFrom"];

    /// <summary>Reads the content of an <c>m:oMath</c> element.</summary>
    public static FormulaNode Read(XElement root, List<Diagnostic> d)
    {
        // Bounds all reader recursion (and the AST depth seen by LatexWriter) by the XML depth.
        NestingGuard.CheckDepth(root, -1);
        return ReadContent(root, d);
    }

    /// <summary>Reads the children of a container element (<c>m:oMath</c>, <c>m:e</c>, <c>m:num</c>, …) as one row.</summary>
    internal static FormulaNode ReadContent(XElement? container, List<Diagnostic> d, bool inEqArrayRow = false)
    {
        var items = new List<FormulaNode>();
        if (container != null) ReadInto(container, items, d, inEqArrayRow);
        return LatexParser.Rowify(items);
    }

    /// <summary>
    /// A non-<c>m</c> element among math content: Word wrappers are read through (<see cref="TransparentW"/>) or
    /// skipped (<see cref="DeletedW"/>); any other element is skipped, with a warning when it holds math.
    /// </summary>
    private static void ReadForeign(XElement el, List<FormulaNode> items, List<Diagnostic> d, bool inEqArrayRow)
    {
        if (el.Name.Namespace == W && TransparentW.Contains(el.Name.LocalName))
        {
            ReadInto(el, items, d, inEqArrayRow);
            return;
        }
        if (el.Name.Namespace == W && DeletedW.Contains(el.Name.LocalName)) return;
        if (el.Descendants().Any(e => e.Name.Namespace == M))
            d.Add(new Diagnostic("unsupported-omml", $"unsupported element {el.Name.LocalName} ({el.Name.NamespaceName}) containing math; its content is dropped", -1));
    }

    private static void ReadInto(XElement container, List<FormulaNode> items, List<Diagnostic> d, bool inEqArrayRow = false)
    {
        NestingGuard.EnsureStack(-1);
        foreach (var el in container.Elements())
        {
            if (el.Name.Namespace != M)
            {
                ReadForeign(el, items, d, inEqArrayRow);
                continue;
            }
            switch (el.Name.LocalName)
            {
                case "ctrlPr" or "argPr":
                    break;
                case "r":
                    ReadRun(el, items, d, inEqArrayRow);
                    break;
                case "brk": // schema-invalid here (m:brk belongs in m:rPr, see ReadRun), but tolerated with the same diagnostic
                    d.Add(new Diagnostic("lossy", "manual break (m:brk) is dropped", -1));
                    break;
                case "f":
                    items.Add(ReadFrac(el, d));
                    break;
                case "d":
                    items.Add(ReadDelimited(el, d));
                    break;
                case "sSub":
                    items.Add(ReadSubSup(el, true, false, d));
                    break;
                case "sSup":
                    items.Add(ReadSubSup(el, false, true, d));
                    break;
                case "sSubSup":
                    items.Add(ReadSubSup(el, true, true, d));
                    break;
                case "sPre":
                    items.Add(ReadPreScript(el, d));
                    break;
                case "rad":
                    items.Add(ReadRadical(el, d));
                    break;
                case "nary":
                    items.Add(ReadNary(el, d));
                    break;
                case "func":
                    items.Add(ReadFunction(el, d));
                    break;
                case "m":
                    items.Add(ReadMatrix(el, d));
                    break;
                case "eqArr":
                    items.Add(ReadEqArray(el, d));
                    break;
                case "acc":
                    items.Add(ReadAccent(el, d));
                    break;
                case "bar":
                    items.Add(ReadBar(el, d));
                    break;
                case "groupChr":
                    items.Add(ReadGroupChar(el, d));
                    break;
                case "limLow":
                case "limUpp":
                    items.Add(ReadLimit(el, d));
                    break;
                case "borderBox":
                    items.Add(ReadBorderBox(el, d));
                    break;
                case "box":
                    if (el.Element(M + "e") is { } boxBody) ReadInto(boxBody, items, d);
                    break;
                case "phant":
                    items.Add(ReadPhantom(el, d));
                    break;
                // Feature slices add cases here, e.g. `case "f": items.Add(ReadFrac(el, d)); break;`
                default:
                    d.Add(new Diagnostic("unsupported-omml", $"unsupported OMML element m:{el.Name.LocalName}; reading its content", -1));
                    ReadInto(el, items, d);
                    break;
            }
        }
    }
}
