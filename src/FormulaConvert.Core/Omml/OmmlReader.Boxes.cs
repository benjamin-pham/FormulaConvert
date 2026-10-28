using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    /// <summary>An on/off property child: absent is <paramref name="absent"/>; present without <c>m:val</c> is on; <c>0</c>/<c>off</c>/<c>false</c> are off.</summary>
    private static bool ReadOnOff(XElement? pr, string name, bool absent)
    {
        var el = pr?.Element(M + name);
        if (el == null) return absent;
        var val = (string?)el.Attribute(M + "val");
        return val == null || val.Trim().ToLowerInvariant() is not ("0" or "off" or "false" or "f");
    }

    /// <summary><c>m:borderBox</c>: an edge is drawn unless its <c>hideX</c> is on; strikes are drawn when on.</summary>
    private static FormulaNode ReadBorderBox(XElement el, List<Diagnostic> d)
    {
        var pr = el.Element(M + "borderBoxPr");
        var edges = BoxEdges.None;
        if (!ReadOnOff(pr, "hideTop", false)) edges |= BoxEdges.Top;
        if (!ReadOnOff(pr, "hideBot", false)) edges |= BoxEdges.Bottom;
        if (!ReadOnOff(pr, "hideLeft", false)) edges |= BoxEdges.Left;
        if (!ReadOnOff(pr, "hideRight", false)) edges |= BoxEdges.Right;
        if (ReadOnOff(pr, "strikeH", false)) edges |= BoxEdges.StrikeH;
        if (ReadOnOff(pr, "strikeV", false)) edges |= BoxEdges.StrikeV;
        if (ReadOnOff(pr, "strikeBLTR", false)) edges |= BoxEdges.StrikeUp;
        if (ReadOnOff(pr, "strikeTLBR", false)) edges |= BoxEdges.StrikeDown;
        return new BorderBox(edges, ReadContent(el.Element(M + "e"), d));
    }

    /// <summary><c>m:phant</c>; <c>m:show</c> defaults to on, the zero flags to off.</summary>
    private static FormulaNode ReadPhantom(XElement el, List<Diagnostic> d)
    {
        var pr = el.Element(M + "phantPr");
        return new Phantom(ReadOnOff(pr, "show", true), ReadOnOff(pr, "zeroWid", false), ReadOnOff(pr, "zeroAsc", false),
            ReadOnOff(pr, "zeroDesc", false), ReadContent(el.Element(M + "e"), d));
    }
}
