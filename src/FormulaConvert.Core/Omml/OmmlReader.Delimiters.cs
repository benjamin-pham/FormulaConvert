using System.Text;
using System.Xml.Linq;
using FormulaConvert.Ast;
using FormulaConvert.Latex;

namespace FormulaConvert.Omml;

internal static partial class OmmlReader
{
    private const string AsciiDelimiters = "()[]{}|/<>";

    /// <summary>
    /// <c>m:d</c>: missing <c>begChr</c>/<c>endChr</c> are parentheses, and several parts without <c>sepChr</c> are
    /// separated by <c>|</c>. <c>m:grow</c> has no AST counterpart and is ignored.
    /// </summary>
    private static FormulaNode ReadDelimited(XElement el, List<Diagnostic> d)
    {
        var props = el.Element(M + "dPr");
        var parts = el.Elements(M + "e").Select(e => ReadSlot(e, d)).ToList();
        if (parts.Count == 0) parts.Add(new Placeholder());
        var open = ReadDelimiterChar(props?.Element(M + "begChr"), "(", d);
        var close = ReadDelimiterChar(props?.Element(M + "endChr"), ")", d);
        var separator = parts.Count > 1 ? ReadDelimiterChar(props?.Element(M + "sepChr"), "|", d) : "";
        return new Delimited(open, close, separator, parts);
    }

    /// <summary>The delimiter character, or the default when the element or its value is missing ("" = absent).</summary>
    private static string ReadDelimiterChar(XElement? chr, string fallback, List<Diagnostic> d)
    {
        var value = (string?)chr?.Attribute(M + "val");
        if (value == null) return fallback;
        if (value.Length == 0) return "";
        var rune = Rune.GetRuneAt(value, 0);
        bool single = rune.Utf16SequenceLength == value.Length;
        bool supported = rune.Value >= 0x80 || AsciiDelimiters.Contains((char)rune.Value);
        if (!single || !supported)
        {
            d.Add(new Diagnostic("lossy", $"delimiter \"{value}\" is not a supported delimiter character; written as absent", -1));
            return "";
        }
        return rune.ToString();
    }
}
