using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class DelimiterTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    /// <summary>m:d; m:dPr holds only the non-default begChr/endChr, and sepChr when sep is not null.</summary>
    private static string Dl(string open, string close, string? sep, params string[] parts)
    {
        var props = (open == "(" ? "" : $"<m:begChr m:val=\"{open}\"/>")
            + (sep == null ? "" : $"<m:sepChr m:val=\"{sep}\"/>")
            + (close == ")" ? "" : $"<m:endChr m:val=\"{close}\"/>");
        return "<m:d>" + (props.Length == 0 ? "" : $"<m:dPr>{props}</m:dPr>")
            + string.Concat(parts.Select(p => $"<m:e>{p}</m:e>")) + "</m:d>";
    }

    private static string F(string num, string den, string? type = null) =>
        "<m:f>" + (type == null ? "" : $"<m:fPr><m:type m:val=\"{type}\"/></m:fPr>") + $"<m:num>{num}</m:num><m:den>{den}</m:den></m:f>";

    [Fact] public void Parens() => Check("\\left(x\\right)", Dl("(", ")", null, R("x")));
    [Fact] public void Brackets() => Check("\\left[x\\right]", Dl("[", "]", null, R("x")));
    [Fact] public void Lbrace() => Check("\\left\\lbrace x \\right\\rbrace", Dl("{", "}", null, R("x")), "\\left\\lbrace x\\right\\rbrace");
    [Fact] public void Escaped_braces() => Check("\\left\\{ x \\right\\}", Dl("{", "}", null, R("x")), "\\left\\lbrace x\\right\\rbrace");
    [Fact] public void Angles() => Check("\\left\\langle x\\right\\rangle", Dl("⟨", "⟩", null, R("x")));
    [Fact] public void Floor() => Check("\\left\\lfloor x\\right\\rfloor", Dl("⌊", "⌋", null, R("x")));
    [Fact] public void Ceil() => Check("\\left\\lceil x\\right\\rceil", Dl("⌈", "⌉", null, R("x")));

    [Theory]
    [InlineData("\\left| x \\right|")]
    [InlineData("\\left\\vert x\\right\\vert")]
    public void Bars(string latex) => Check(latex, Dl("|", "|", null, R("x")), "\\left|x\\right|");

    [Theory]
    [InlineData("\\left\\| x \\right\\|")]
    [InlineData("\\left\\Vert x\\right\\Vert")]
    [InlineData("\\left\\lVert x\\right\\rVert")]
    public void Double_bars(string latex) => Check(latex, Dl("‖", "‖", null, R("x")), "\\left\\Vert x\\right\\Vert");

    [Fact] public void Absent_close() => Check("\\left( x \\right.", Dl("(", "", null, R("x")), "\\left(x\\right.");
    [Fact] public void Absent_open() => Check("\\left. x \\right)", Dl("", ")", null, R("x")), "\\left.x\\right)");
    [Fact] public void Middle() => Check("\\left(a\\middle|b\\right)", Dl("(", ")", "|", R("a"), R("b")));
    [Fact] public void Middle_absent() => Check("\\left(a\\middle.b\\right)", Dl("(", ")", "", R("a"), R("b")));
    [Fact] public void Lparen_rbrack() => Check("\\left\\lparen \\frac1x \\right\\rbrack", Dl("(", "]", null, F(R("1"), R("x"))), "\\left(\\frac{1}{x}\\right]");
    [Fact] public void Nested() => Check("\\left(\\left[x\\right]\\right)", Dl("(", ")", null, Dl("[", "]", null, R("x"))));
    [Fact] public void Empty_body() => Check("\\left(\\right)", Dl("(", ")", null, ""));
    [Fact] public void With_scripts() => Check("\\left(x\\right)^2", "<m:sSup><m:e>" + Dl("(", ")", null, R("x")) + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>");
    [Fact] public void Fraction_inside() => Check("\\left(a\\over b\\right)", Dl("(", ")", null, F(R("a"), R("b"))), "\\left(\\frac{a}{b}\\right)");

    [Fact]
    public void Non_default_brackets_around_atop() =>
        Check("\\left[{a \\atop b}\\right]", Dl("[", "]", null, F(R("a"), R("b"), "noBar")));

    [Fact]
    public void Binomial_with_separator_is_not_a_binomial() =>
        Check("\\left({a \\atop b}\\middle|c\\right)", Dl("(", ")", "|", F(R("a"), R("b"), "noBar"), R("c")));

    [Fact] public void Binom_still_works() => Check("\\binom{n}{k}", Dl("(", ")", null, F(R("n"), R("k"), "noBar")));

    [Fact]
    public void Sized_left_right()
    {
        Check("\\bigl( x \\bigr)", R("(x)"), "(x)");
        var diagnostics = FormulaConverter.LatexToOmml("\\bigl( x \\bigr)").Diagnostics;
        Assert.Equal(2, diagnostics.Count); // one per sized delimiter
        Assert.All(diagnostics, x => Assert.Equal("lossy", x.Code));
    }

    [Theory]
    [InlineData("\\big(", "(")]
    [InlineData("\\Big[", "[")]
    [InlineData("\\bigg|", "|")]
    [InlineData("\\Bigg\\langle", "⟨")]
    [InlineData("\\bigm|", "|")]
    [InlineData("\\Bigm\\Vert", "‖")]
    [InlineData("\\biggl\\{", "{")]
    [InlineData("\\Biggr\\rbrace", "}")]
    [InlineData("\\Biggm/", "/")]
    public void Sized_delimiters_are_bare_runs_with_one_lossy(string latex, string chr)
    {
        var r = FormulaConverter.LatexToOmml(latex);
        Assert.True(SameXml(OMath(R(chr)), r.Output), r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);
        Assert.Equal(0, d.Position);
    }

    [Fact]
    public void Differing_middle_keeps_first_and_warns()
    {
        var r = FormulaConverter.LatexToOmml("\\left(a\\middle|b\\middle/c\\right)");
        Assert.True(SameXml(OMath(Dl("(", ")", "|", R("a"), R("b"), R("c"))), r.Output), r.Output);
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("lossy", d.Code);
    }

    [Theory]
    [InlineData("\\left(", 0)]
    [InlineData("x+\\left(y", 2)]
    [InlineData("{\\left(y}", 1)]
    [InlineData("\\left(a\\middle|b", 0)]
    [InlineData("\\right)", 0)]
    [InlineData("x\\right)", 1)]
    [InlineData("\\middle|", 0)]
    [InlineData("{\\left( x}\\right)", 1)]
    [InlineData("\\left{x\\right)", 5)]
    [InlineData("\\left\\foo x\\right)", 5)]
    [InlineData("\\left(x\\right\\foo", 13)]
    [InlineData("\\left", 5)]
    [InlineData("\\bigl", 5)]
    public void Malformed_throws_with_position(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Deep_left_right_nesting_throws_cleanly()
    {
        var latex = string.Concat(Enumerable.Repeat("\\left(", 2000)) + "x" + string.Concat(Enumerable.Repeat("\\right)", 2000));
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Contains("nesting too deep", ex.Message);
    }

    [Fact]
    public void Reader_multiple_parts_without_sepChr_default_to_bar()
    {
        var xml = "<m:d><m:e>" + R("a") + "</m:e><m:e>" + R("b") + "</m:e></m:d>";
        Assert.Equal("\\left(a\\middle|b\\right)", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact]
    public void Reader_empty_sepChr_means_no_separator()
    {
        var xml = "<m:d><m:dPr><m:sepChr m:val=\"\"/></m:dPr><m:e>" + R("a") + "</m:e><m:e>" + R("b") + "</m:e></m:d>";
        Assert.Equal("\\left(a\\middle.b\\right)", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact]
    public void Reader_accepts_full_word_form_with_grow()
    {
        var xml = "<m:d><m:dPr><m:begChr m:val=\"[\"/><m:endChr m:val=\"]\"/><m:grow m:val=\"1\"/><m:ctrlPr/></m:dPr><m:e>" + R("x") + "</m:e></m:d>";
        Assert.Equal("\\left[x\\right]", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact]
    public void Reader_no_dPr_is_parentheses()
    {
        var xml = "<m:d><m:e>" + R("x") + "</m:e></m:d>";
        Assert.Equal("\\left(x\\right)", FormulaConverter.OmmlToLatex(OMath(xml)).Output);
    }

    [Fact]
    public void Reader_empty_d_does_not_crash()
    {
        Assert.Equal("\\left(\\right)", FormulaConverter.OmmlToLatex(OMath("<m:d/>")).Output);
    }

    [Fact]
    public void Reader_unsupported_delimiter_character_is_dropped_with_lossy()
    {
        var xml = "<m:d><m:dPr><m:begChr m:val=\"ab\"/></m:dPr><m:e>" + R("x") + "</m:e></m:d>";
        var r = FormulaConverter.OmmlToLatex(OMath(xml));
        Assert.Equal("\\left.x\\right)", r.Output);
        Assert.Contains(r.Diagnostics, x => x.Code == "lossy");
    }

    [Fact]
    public void Nary_operand_delimited_round_trips()
    {
        var omml = FormulaConverter.LatexToOmml("\\sum\\left(x\\right)").Output;
        Assert.Equal("\\sum\\left(x\\right)", FormulaConverter.OmmlToLatex(omml).Output);
    }
}
