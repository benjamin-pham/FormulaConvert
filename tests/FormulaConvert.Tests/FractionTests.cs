using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class FractionTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    /// <summary>m:f with optional type (null = default bar, no m:fPr).</summary>
    private static string F(string num, string den, string? type = null) =>
        "<m:f>" + (type == null ? "" : $"<m:fPr><m:type m:val=\"{type}\"/></m:fPr>") + $"<m:num>{num}</m:num><m:den>{den}</m:den></m:f>";

    /// <summary>m:d with default parentheses.</summary>
    private static string D(string e) => $"<m:d><m:e>{e}</m:e></m:d>";

    [Fact] public void Frac() => Check("\\frac{a}{b}", F(R("a"), R("b")));

    [Theory]
    [InlineData("\\tfrac{a}{b}")]
    [InlineData("\\cfrac{a}{b}")]
    public void Frac_variants(string latex) => Check(latex, F(R("a"), R("b")), "\\frac{a}{b}");

    [Fact] public void Cfrac_optional_alignment() => Check("\\cfrac[l]{1}{x+1}", F(R("1"), R("x+1")), "\\frac{1}{x+1}");

    [Fact]
    public void Dfrac_warns_lossy_once()
    {
        Check("\\dfrac{a}{b}", F(R("a"), R("b")), "\\frac{a}{b}");
        var d = Assert.Single(FormulaConverter.LatexToOmml("\\dfrac{a}{b}").Diagnostics);
        Assert.Equal("lossy", d.Code);
        Assert.Equal(0, d.Position);
    }

    [Fact] public void Frac_no_warning() => Assert.Empty(FormulaConverter.LatexToOmml("\\frac{a}{b}").Diagnostics);

    [Fact] public void Over() => Check("a \\over b", F(R("a"), R("b")), "\\frac{a}{b}");
    [Fact] public void Over_in_group() => Check("{x+1 \\over 2}", F(R("x+1"), R("2")), "\\frac{x+1}{2}");
    [Fact] public void Over_in_group_with_context() => Check("1+{a \\over b}", R("1+") + F(R("a"), R("b")), "1+\\frac{a}{b}");

    [Fact] public void Binom() => Check("\\binom{n}{k}", D(F(R("n"), R("k"), "noBar")));

    [Theory]
    [InlineData("\\dbinom{n}{k}")]
    [InlineData("\\tbinom{n}{k}")]
    [InlineData("n \\choose k")]
    public void Binom_variants(string latex) => Check(latex, D(F(R("n"), R("k"), "noBar")), "\\binom{n}{k}");

    [Fact] public void Atop() => Check("a \\atop b", F(R("a"), R("b"), "noBar"), "{a \\atop b}");
    [Fact]
    public void Nicefrac()
    {
        Check("\\nicefrac{a}{b}", F(R("a"), R("b"), "skw"));
        Assert.Empty(FormulaConverter.LatexToOmml("\\nicefrac{a}{b}").Diagnostics);
        Assert.Empty(FormulaConverter.OmmlToLatex(OMath(F(R("a"), R("b"), "skw"))).Diagnostics);
    }
    [Fact] public void Pdiff() => Check("\\pdiff{f}{x}", F(R("∂f"), R("∂x")), "\\frac{\\partial f}{\\partial x}");
    [Fact] public void Empty_frac() => Check("\\frac{}{}", F("", ""));
    [Fact] public void Nested_frac() => Check("\\frac{a}{\\frac{b}{c}}", F(R("a"), F(R("b"), R("c"))));

    [Fact]
    public void Genfrac_binomial() =>
        Check("\\genfrac{(}{)}{0pt}{}{a}{b}", D(F(R("a"), R("b"), "noBar")), "\\binom{a}{b}");

    [Fact]
    public void Overwithdelims_binomial() =>
        Check("{a \\atopwithdelims ( ) b}", D(F(R("a"), R("b"), "noBar")), "\\binom{a}{b}");

    [Theory]
    [InlineData("a \\over b \\over c", 10)]
    [InlineData("\\frac{a}", 8)]
    public void Unsupported_or_malformed_throws(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Reader_lin_single_char_sides()
    {
        var r = FormulaConverter.OmmlToLatex(OMath(F(R("a"), R("b"), "lin")));
        Assert.Equal("a/b", r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);

        var omml = FormulaConverter.LatexToOmml(r.Output);
        Assert.NotNull(omml.Output);
    }

    [Fact]
    public void Reader_lin_multi_char_side_braced()
    {
        var r = FormulaConverter.OmmlToLatex(OMath(F(R("a+b"), R("c"), "lin")));
        Assert.Equal("{a+b}/c", r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);

        var omml = FormulaConverter.LatexToOmml(r.Output);
        Assert.NotNull(omml.Output);
    }

    [Fact]
    public void Reader_lin_in_superscript_base_braced()
    {
        var xml = "<m:sSup><m:e>" + F(R("a"), R("b"), "lin") + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>";
        var r = FormulaConverter.OmmlToLatex(OMath(xml));
        Assert.Equal("{a/b}^2", r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);

        var omml = FormulaConverter.LatexToOmml(r.Output);
        Assert.Contains("<m:sSup>", omml.Output);
        Assert.Contains("<m:e>" + R("a/b") + "</m:e>", omml.Output);
        Assert.Contains("<m:sup>" + R("2") + "</m:sup>", omml.Output);
    }

    [Fact]
    public void Reader_lin_in_nested_fraction_numerator()
    {
        var xml = F(F(R("a"), R("b"), "lin"), R("c"));
        var r = FormulaConverter.OmmlToLatex(OMath(xml));
        Assert.Equal("\\frac{a/b}{c}", r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);

        var omml = FormulaConverter.LatexToOmml(r.Output);
        Assert.NotNull(omml.Output);
    }

    [Fact]
    public void Reader_lin_as_nary_operand_braced()
    {
        var xml = "<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:subHide m:val=\"1\"/><m:supHide m:val=\"1\"/></m:naryPr><m:sub/><m:sup/><m:e>"
            + F(R("a"), R("b"), "lin") + "</m:e></m:nary>";
        var r = FormulaConverter.OmmlToLatex(OMath(xml));
        Assert.Equal("\\sum{a/b}", r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);

        var omml = FormulaConverter.LatexToOmml(r.Output);
        Assert.NotNull(omml.Output);
    }

    [Fact]
    public void Reader_accepts_full_word_form()
    {
        var xml = "<m:f><m:fPr><m:type m:val=\"bar\"/><m:ctrlPr/></m:fPr><m:num>" + R("a") + "</m:num><m:den>" + R("b") + "</m:den></m:f>";
        Assert.Equal("\\frac{a}{b}", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact] public void Reader_binom_in_default_parens_with_explicit_chars()
    {
        var xml = "<m:d><m:dPr><m:begChr m:val=\"(\"/><m:endChr m:val=\")\"/></m:dPr><m:e>" + F(R("n"), R("k"), "noBar") + "</m:e></m:d>";
        Assert.Equal("\\binom{n}{k}", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact]
    public void Reader_small_fraction_ignores_argPr()
    {
        var xml = "<m:f><m:fPr><m:type m:val=\"bar\"/></m:fPr>"
            + "<m:num><m:argPr><m:argSz m:val=\"-2\"/></m:argPr>" + R("a") + "</m:num>"
            + "<m:den><m:argPr><m:argSz m:val=\"-2\"/></m:argPr>" + R("b") + "</m:den></m:f>";
        var r = FormulaConverter.OmmlToLatex(OMath(xml));
        Assert.Equal("\\frac{a}{b}", r.Output);
        Assert.Empty(r.Diagnostics);
    }
}
