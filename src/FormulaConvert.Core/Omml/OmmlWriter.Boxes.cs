using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    private const BoxEdges AllEdges = BoxEdges.Top | BoxEdges.Bottom | BoxEdges.Left | BoxEdges.Right;

    private static XElement Flag(string name, string val = "1") => new(M + name, new XAttribute(M + "val", val));

    /// <summary><c>m:borderBox</c>; <c>hideX</c> flags for edges that are not drawn, none at all for a plain box.</summary>
    private static XElement WriteBorderBox(BorderBox box)
    {
        var pr = new XElement(M + "borderBoxPr");
        if ((box.Edges & BoxEdges.Top) == 0) pr.Add(Flag("hideTop"));
        if ((box.Edges & BoxEdges.Bottom) == 0) pr.Add(Flag("hideBot"));
        if ((box.Edges & BoxEdges.Left) == 0) pr.Add(Flag("hideLeft"));
        if ((box.Edges & BoxEdges.Right) == 0) pr.Add(Flag("hideRight"));
        if ((box.Edges & BoxEdges.StrikeH) != 0) pr.Add(Flag("strikeH"));
        if ((box.Edges & BoxEdges.StrikeV) != 0) pr.Add(Flag("strikeV"));
        if ((box.Edges & BoxEdges.StrikeUp) != 0) pr.Add(Flag("strikeBLTR"));
        if ((box.Edges & BoxEdges.StrikeDown) != 0) pr.Add(Flag("strikeTLBR"));
        var e = new XElement(M + "borderBox");
        if (box.Edges != AllEdges) e.Add(pr);
        e.Add(Element("e", box.Body));
        return e;
    }

    private static XElement WritePhantom(Phantom ph)
    {
        var pr = new XElement(M + "phantPr");
        if (!ph.Show) pr.Add(Flag("show", "0"));
        if (ph.ZeroWidth) pr.Add(Flag("zeroWid"));
        if (ph.ZeroAscent) pr.Add(Flag("zeroAsc"));
        if (ph.ZeroDescent) pr.Add(Flag("zeroDesc"));
        var e = new XElement(M + "phant");
        if (pr.HasElements) e.Add(pr);
        e.Add(Element("e", ph.Body));
        return e;
    }
}
