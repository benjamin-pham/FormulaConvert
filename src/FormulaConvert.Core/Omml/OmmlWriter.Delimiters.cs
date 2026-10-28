using System.Xml.Linq;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static partial class OmmlWriter
{
    /// <summary>
    /// <c>m:d</c>; <c>m:dPr</c> holds <c>begChr</c>/<c>endChr</c> only when not the default parenthesis, and
    /// <c>sepChr</c> whenever there are several parts (so an absent separator stays absent).
    /// </summary>
    private static XElement WriteDelimited(Delimited d)
    {
        var props = new XElement(M + "dPr");
        if (d.Open != "(") props.Add(Chr("begChr", d.Open));
        if (d.Parts.Count > 1) props.Add(Chr("sepChr", d.Separator));
        if (d.Close != ")") props.Add(Chr("endChr", d.Close));
        var e = new XElement(M + "d");
        if (props.HasElements) e.Add(props);
        if (d.Parts.Count == 0) e.Add(Element("e", FormulaRow.Empty));
        foreach (var part in d.Parts) e.Add(Element("e", part));
        return e;
    }

    private static XElement Chr(string name, string value) => new(M + name, new XAttribute(M + "val", value));
}
