using System.Xml.Linq;

namespace FormulaConvert.Tests.Harness;

/// <summary>Helpers for building OMML fragments in tests.</summary>
public static class Xml
{
    public const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary><c>&lt;m:r&gt;{rPr}&lt;m:t&gt;{text}&lt;/m:t&gt;&lt;/m:r&gt;</c>; <paramref name="rPr"/> is wrapped in <c>m:rPr</c>. Text is XML-escaped.</summary>
    public static string R(string text, string? rPr = null) =>
        "<m:r>" + (rPr == null ? "" : "<m:rPr>" + rPr + "</m:rPr>") + "<m:t>" + Escape(text) + "</m:t></m:r>";

    /// <summary>Wraps an inner OMML fragment in <c>m:oMath</c> with the <c>m</c> namespace declared.</summary>
    public static string OMath(string inner) => $"<m:oMath xmlns:m=\"{MathNamespace}\">{inner}</m:oMath>";

    public static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Parses XML and removes whitespace-only text nodes (except inside <c>m:t</c>) and namespace declarations.</summary>
    public static XElement Normalize(string xml)
    {
        var root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        XNamespace m = MathNamespace;
        root.DescendantNodesAndSelf().OfType<XText>()
            .Where(t => string.IsNullOrWhiteSpace(t.Value) && t.Parent?.Name != m + "t")
            .ToList().ForEach(t => t.Remove());
        foreach (var e in root.DescendantsAndSelf())
            e.Attributes().Where(a => a.IsNamespaceDeclaration).ToList().ForEach(a => a.Remove());
        return root;
    }

    /// <summary>True when the two XML strings are equal after <see cref="Normalize"/>.</summary>
    public static bool SameXml(string expected, string actual) =>
        XNode.DeepEquals(Normalize(expected), Normalize(actual));
}
