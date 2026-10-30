using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class OutOfScopeTests
{
    /// <summary>Parses <paramref name="latex"/>, expects the OMML and exactly <paramref name="count"/> out-of-scope diagnostics.</summary>
    private static ConvertResult Check(string latex, string ommlInner, int count = 1, string code = "out-of-scope")
    {
        var r = FormulaConverter.LatexToOmml(latex);
        Assert.True(SameXml(OMath(ommlInner), r.Output), $"mismatch for `{latex}`\n  expected: {OMath(ommlInner)}\n  actual:   {r.Output}");
        Assert.Equal(count, r.Diagnostics.Count);
        Assert.All(r.Diagnostics, d => Assert.Equal(code, d.Code));
        return r;
    }

    [Theory]
    [InlineData("\\textcolor{red}{x+1}", "x+1")]
    [InlineData("\\colorbox{yellow}{x}", "x")]
    [InlineData("\\fcolorbox{a}{b}{x}", "x")]
    [InlineData("\\mathcolor{#cd0030}{x}", "x")]
    [InlineData("{\\color{blue} x+1}", "x+1")]
    [InlineData("\\color{#cd0030} x", "x")]
    [InlineData("\\Large x", "x")]
    [InlineData("\\tiny{x}", "x")]
    [InlineData("\\Huge x", "x")]
    [InlineData("\\href{https://x.y/z}{x}", "x")]
    [InlineData("\\class{c}{x}", "x")]
    [InlineData("\\cssId{i}{x}", "x")]
    [InlineData("\\htmlData{k=v}{x}", "x")]
    [InlineData("\\htmlStyle{color:red}{x}", "x")]
    [InlineData("\\texttip{x}{tip}", "x")]
    [InlineData("\\mathtip{x}{y}", "x")]
    [InlineData("\\error{x}", "x")]
    [InlineData("\\displaystyle x", "x")]
    [InlineData("\\textstyle x", "x")]
    [InlineData("\\scriptstyle x", "x")]
    [InlineData("\\scriptscriptstyle x", "x")]
    [InlineData("\\mathop{x}", "x")]
    [InlineData("\\mathrel{xy}", "xy")]
    [InlineData("\\mathbin{xy}", "xy")]
    [InlineData("\\mathord{x}", "x")]
    [InlineData("\\mathopen{x}", "x")]
    [InlineData("\\mathclose{x}", "x")]
    [InlineData("\\mathpunct{x}", "x")]
    [InlineData("\\mathinner{x}", "x")]
    [InlineData("\\overgroup{AB}", "AB")]
    [InlineData("\\underlinesegment{AB}", "AB")]
    [InlineData("\\utilde{AB}", "AB")]
    [InlineData("\\overarc{AB}", "AB")]
    [InlineData("\\Overrightarrow{AB}", "AB")]
    [InlineData("\\mathllap{x}", "x")]
    [InlineData("\\fbox{x}", "x")]
    [InlineData("\\raisebox{1em}{x}", "x")]
    [InlineData("\\bbox[5px,border:2px solid red]{x}", "x")]
    [InlineData("\\textsc{x}", "x")]
    [InlineData("\\emph{x}", "x")]
    [InlineData("\\bfseries x", "x")]
    [InlineData("\\fontfamily{ptm} x", "x")]
    [InlineData("\\fontsize{1}{2} x", "x")]
    [InlineData("{\\bf x}", "x")]
    [InlineData("\\relax x", "x")]
    public void Wrapper_is_stripped_and_content_kept(string latex, string kept)
    {
        var r = Check(latex, R(kept));
        Assert.Equal(latex.IndexOf((char)92), r.Diagnostics[0].Position);
    }

    [Fact]
    public void Diagnostic_message_names_the_command()
    {
        var r = Check("x\\textcolor{red}{y}", R("xy"));
        var d = Assert.Single(r.Diagnostics);
        Assert.Equal("\\textcolor is not supported by OMML", d.Message);
        Assert.Equal(1, d.Position);
    }

    [Fact]
    public void Rule_and_ce()
    {
        Check("a\\rule{1em}{2em}b", R("ab"));
        Check("a\\rule[1em]{1em}{2em}b", R("ab"));
        Check("\\ce{H2O}", R("H2O", "<m:nor/>"));
    }

    [Theory]
    [InlineData("a\\hspace{1em}b")]
    [InlineData("a\\hspace*{1em}b")]
    public void Spacing_is_dropped(string latex) => Check(latex, R("ab"));

    [Fact]
    public void Relax_is_dropped() => Check("\\relax", "");

    [Theory]
    [InlineData("\\limits", 0)]
    [InlineData("a\\limits", 1)]
    [InlineData("\\nolimits", 0)]
    [InlineData("a\\nolimits", 1)]
    [InlineData("a\\displaylimits", 1)]
    public void Stray_limits_modifiers_are_out_of_scope(string latex, int position)
    {
        var r = Check(latex, latex.StartsWith('a') ? R("a") : "");
        Assert.Equal(position, r.Diagnostics[0].Position);
    }

    [Fact]
    public void Missing_argument_is_an_error()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\textcolor{red}"));
        Assert.Equal(15, ex.Position);
    }

    [Fact]
    public void Nested_wrappers_warn_each() => Check("\\textcolor{red}{\\Large x}", R("x"), 2);

    [Fact]
    public void Kern_bare_dimension()
    {
        var res = FormulaConverter.LatexToOmml("\\kern1em x");
        Assert.Single(res.Diagnostics, d => d.Code == "out-of-scope");
        Assert.Equal(OMath(R("x")), res.Output);
    }

    [Fact]
    public void Lower_bare_dimension()
    {
        var res = FormulaConverter.LatexToOmml("\\lower2pt x");
        Assert.Single(res.Diagnostics, d => d.Code == "out-of-scope");
        Assert.Equal(OMath(R("x")), res.Output);
    }
}
