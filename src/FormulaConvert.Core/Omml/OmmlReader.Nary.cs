using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary><c>m:nary</c>: no <c>m:chr</c> means ∫, no <c>m:limLoc</c> the character's default; hidden scripts are absent.</summary>
    private static FormulaNode ReadNary(XElement el, List<Diagnostic> d)
    {
        var pr = el.Element(M + "naryPr");
        var chrEl = pr?.Element(M + "chr");
        string chr = chrEl == null ? "∫" : (string?)chrEl.Attribute(M + "val") ?? "";
        bool subSup = NaryTable.TryGetByChar(chr, out var info) && info.SubSupDefault;
        if ((string?)pr?.Element(M + "limLoc")?.Attribute(M + "val") is { } loc)
            subSup = loc == "subSup";
        var sub = IsOn(pr?.Element(M + "subHide")) ? null : ReadSlot(el.Element(M + "sub"), d);
        var sup = IsOn(pr?.Element(M + "supHide")) ? null : ReadSlot(el.Element(M + "sup"), d);
        return new Nary(chr, sub, sup, ReadContent(el.Element(M + "e"), d), subSup);
    }

    /// <summary><c>m:func</c>: the name (runs joined, upright; optionally with scripts or a limit) and the argument.</summary>
    private static FormulaNode ReadFunction(XElement el, List<Diagnostic> d)
    {
        var fName = el.Element(M + "fName");
        var inner = fName?.Elements().Where(e => e.Name.Namespace == M && e.Name.LocalName != "ctrlPr").ToList();
        FormulaNode name;
        if (inner is [{ Name.LocalName: "limLow" or "limUpp" } lim])
            name = new Limit(PlainName(ReadContent(lim.Element(M + "e"), d)), ReadContent(lim.Element(M + "lim"), d), lim.Name.LocalName == "limUpp");
        else
            name = PlainName(ReadContent(fName, d));
        return new FunctionApply(name, ReadContent(el.Element(M + "e"), d));
    }

    /// <summary>Joins the runs of a name into one upright run (looking through a script's base).</summary>
    private static FormulaNode PlainName(FormulaNode node)
    {
        switch (node)
        {
            case Script s:
                return s with { Base = PlainName(s.Base) };
            case Run r:
                return new Run(r.Text, new RunStyle(Sty.Plain));
            case FormulaRow row when row.Items.Count > 0 && row.Items.All(i => i is Run):
                return new Run(string.Concat(row.Items.Cast<Run>().Select(r => r.Text)), new RunStyle(Sty.Plain));
            default:
                return node;
        }
    }

    /// <summary>An on/off element: present without <c>m:val</c> means on.</summary>
    private static bool IsOn(XElement? el) =>
        el != null && ((string?)el.Attribute(M + "val") ?? "1") is "1" or "true" or "on";
}
