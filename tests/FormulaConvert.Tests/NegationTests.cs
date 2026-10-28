using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class NegationTests
{
    private static string Text(string latex, out int diagnostics)
    {
        var r = FormulaConverter.LatexToOmml(latex);
        diagnostics = r.Diagnostics.Count;
        return string.Concat(System.Xml.Linq.XElement.Parse(r.Output)
            .Descendants(System.Xml.Linq.XName.Get("t", MathNamespace)).Select(t => t.Value));
    }

    [Theory]
    [InlineData(@"a\not=b", "a≠b")]
    [InlineData(@"a\not\in B", "a∉B")]
    [InlineData(@"\not<", "≮")]
    [InlineData(@"\not>", "≯")]
    [InlineData(@"a\not\subseteq b", "a⊈b")]
    [InlineData(@"a\not\equiv b", "a≢b")]
    [InlineData(@"a\not\cong b", "a≇b")]
    [InlineData(@"a\not\le b", "a≰b")]
    [InlineData(@"\not\exists", "∄")]
    [InlineData(@"a\not\mid b", "a∤b")]
    public void Negates_known_relation_without_diagnostics(string latex, string expected)
    {
        Assert.Equal(expected, Text(latex, out var n));
        Assert.Equal(0, n);
    }

    [Fact]
    public void Other_token_warns_once_and_is_kept()
    {
        Assert.Equal("a+b", Text(@"a\not+b", out var n));
        Assert.Equal(1, n);
    }

    [Fact]
    public void Alone_warns_once_and_is_empty()
    {
        Assert.Equal("", Text(@"\not", out var n));
        Assert.Equal(1, n);
    }

    [Fact]
    public void Unknown_following_command_is_an_error() =>
        Assert.ThrowsAny<Exception>(() => FormulaConverter.LatexToOmml(@"a\not\foo"));

    [Fact]
    public void Negated_equals_writes_canonical_ne() =>
        Assert.Equal(@"a\ne b", FormulaConverter.OmmlToLatex(OMath(R("a") + R("≠") + R("b"))).Output);
}
