using System.Xml;
using System.Xml.Linq;
using FormulaConvert.Latex;
using FormulaConvert.Omml;

namespace FormulaConvert;

/// <summary>Converts between OMML (Office Math Markup) and LaTeX in MathLive syntax.</summary>
public static class FormulaConverter
{
    /// <summary>
    /// Parses LaTeX and returns an unindented <c>&lt;m:oMath xmlns:m="…"&gt;…&lt;/m:oMath&gt;</c> string.
    /// Throws <see cref="FormulaParseException"/> (with the source position) on syntax errors.
    /// </summary>
    public static ConvertResult LatexToOmml(string latex)
    {
        ArgumentNullException.ThrowIfNull(latex);
        var diagnostics = new List<Diagnostic>();
        var node = LatexParser.Parse(latex, diagnostics);
        var xml = OmmlWriter.Write(node).ToString(SaveOptions.DisableFormatting);
        return new ConvertResult(xml, diagnostics);
    }

    /// <summary>
    /// Converts OMML (an <c>m:oMath</c>, an <c>m:oMathPara</c>, or any XML containing <c>m:oMath</c> elements) to LaTeX;
    /// several formulas are joined with <c>\n</c>. Malformed XML or no <c>m:oMath</c> → <see cref="FormulaParseException"/> at position -1.
    /// </summary>
    public static ConvertResult OmmlToLatex(string omml)
    {
        ArgumentNullException.ThrowIfNull(omml);
        XElement root;
        try
        {
            root = XElement.Parse(omml, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new FormulaParseException($"malformed XML: {ex.Message}", -1);
        }

        var oMathName = OmmlNames.M + "oMath";
        var formulas = root.DescendantsAndSelf(oMathName)
            .Where(e => !e.Ancestors(oMathName).Any())
            .ToList();
        if (formulas.Count == 0)
            throw new FormulaParseException("no m:oMath element found", -1);

        var diagnostics = new List<Diagnostic>();
        var outputs = formulas.Select(f => LatexWriter.Write(OmmlReader.Read(f, diagnostics)));
        return new ConvertResult(string.Join("\n", outputs), diagnostics);
    }
}
