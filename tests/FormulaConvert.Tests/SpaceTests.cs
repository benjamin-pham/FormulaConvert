using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class SpaceTests
{
    private const string Nor = "<m:nor/>";

    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static string NorSpaced(string text) =>
        $"<m:r><m:rPr><m:nor/></m:rPr><m:t xml:space=\"preserve\">{Escape(text)}</m:t></m:r>";

    private static string Spaced(string text) =>
        $"<m:r><m:t xml:space=\"preserve\">{Escape(text)}</m:t></m:r>";

    [Theory]
    [InlineData("a\\,b", "a\u2009b", null)]
    [InlineData("a\\:b", "a\u2005b", null)]
    [InlineData("a\\>b", "a\u2005b", "a\\:b")]
    [InlineData("a\\;b", "a\u2004b", null)]
    [InlineData("a\\quad b", "a\u2003b", null)]
    [InlineData("a\\qquad b", "a\u2003\u2003b", null)]
    [InlineData("a\\enspace b", "a\u2002b", null)]
    [InlineData("a\\enskip b", "a\u2002b", "a\\enspace b")]
    [InlineData("a\\ b", "a\u00A0b", null)]
    [InlineData("a~b", "a\u00A0b", "a\\ b")]
    [InlineData("a\\space b", "a\u00A0b", "a\\ b")]
    [InlineData("a\\nobreakspace b", "a\u00A0b", "a\\ b")]
    public void Bidirectional_spacing_commands(string latex, string expectedText, string? canonical) =>
        Check(latex, R(expectedText), canonical);

    [Theory]
    [InlineData("a\u2006b", "a\\,b")]
    [InlineData("a\u200Ab", "a\\,b")]
    [InlineData("a\u205Fb", "a\\:b")]
    [InlineData("a b", "a\\ b")]
    public void Reverse_spaces_canonicalize(string ommlText, string expectedLatex)
    {
        var latex = FormulaConverter.OmmlToLatex(OMath(R(ommlText))).Output;
        Assert.Equal(expectedLatex, latex);
    }

    [Fact]
    public void Negative_space_is_lossy_and_dropped()
    {
        var r = FormulaConverter.LatexToOmml("a\\!b");
        Assert.True(SameXml(OMath(R("ab")), r.Output), r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);
    }

    [Fact]
    public void Normal_text_preserves_unicode_spaces() =>
        Check("\\text{a\u2009b}", NorSpaced("a\u2009b"));

    [Fact]
    public void Leading_space_preserves_xml_space() =>
        Check("\\quad x", Spaced("\u2003x"));

    [Fact]
    public void Integral_with_thin_space_has_no_diagnostics()
    {
        var r = FormulaConverter.LatexToOmml("\\int f(x)\\,dx");
        Assert.Empty(r.Diagnostics);
    }

    [Fact]
    public void Operatorname_argument_spacing_has_no_unicode_spaces()
    {
        var r = FormulaConverter.LatexToOmml("\\operatorname{arg\\,max}x");
        Assert.DoesNotContain("\u2009", r.Output);
        Assert.Contains("argmax", r.Output);
    }

    [Theory]
    [InlineData("\\,", "\u2009")]
    [InlineData("\\:", "\u2005")]
    [InlineData("\\>", "\u2005")]
    [InlineData("\\;", "\u2004")]
    [InlineData("\\quad", "\u2003")]
    [InlineData("\\qquad", "\u2003\u2003")]
    [InlineData("\\enspace", "\u2002")]
    [InlineData("\\enskip", "\u2002")]
    [InlineData("\\ ", "\u00A0")]
    [InlineData("~", "\u00A0")]
    [InlineData("\\space", "\u00A0")]
    [InlineData("\\nobreakspace", "\u00A0")]
    public void SpaceTable_TryGetText(string command, string expectedText)
    {
        Assert.True(FormulaConvert.Data.SpaceTable.TryGetText(command, out var text));
        Assert.Equal(expectedText, text);
    }

    [Theory]
    [InlineData(0x2009, "\\,")]
    [InlineData(0x2006, "\\,")]
    [InlineData(0x200A, "\\,")]
    [InlineData(0x2005, "\\:")]
    [InlineData(0x205F, "\\:")]
    [InlineData(0x2004, "\\;")]
    [InlineData(0x2003, "\\quad")]
    [InlineData(0x2002, "\\enspace")]
    [InlineData(0x00A0, "\\ ")]
    [InlineData(0x0020, "\\ ")]
    public void SpaceTable_TryGetCommand(int cp, string expectedCommand)
    {
        Assert.True(FormulaConvert.Data.SpaceTable.TryGetCommand(cp, out var command));
        Assert.Equal(expectedCommand, command);
    }

    [Theory]
    [InlineData(0x2009, true)]
    [InlineData(0x2005, true)]
    [InlineData(0x2004, true)]
    [InlineData(0x2003, true)]
    [InlineData(0x2002, true)]
    [InlineData(0x00A0, true)]
    [InlineData(0x0020, true)]
    [InlineData('a', false)]
    [InlineData(0x2212, false)]
    public void SpaceTable_IsSpace(int cp, bool expected) =>
        Assert.Equal(expected, FormulaConvert.Data.SpaceTable.IsSpace(cp));
}
