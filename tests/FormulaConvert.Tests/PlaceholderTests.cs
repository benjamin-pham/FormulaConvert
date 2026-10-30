using FormulaConvert.Ast;
using FormulaConvert.Latex;
using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class PlaceholderTests
{
    private static void Omml(string latex, string inner) =>
        Assert.True(SameXml(OMath(inner), FormulaConverter.LatexToOmml(latex).Output));

    [Fact]
    public void Alone_is_empty_box() =>
        Omml(@"\placeholder{}", "<m:box><m:e></m:e></m:box>");

    [Fact]
    public void In_row_is_empty_box() =>
        Omml(@"a+\placeholder{}", $"{R("a+")}<m:box><m:e></m:e></m:box>");

    [Fact]
    public void Fraction_slot_is_empty() =>
        Omml(@"\frac{\placeholder{}}{2}", $"<m:f><m:num></m:num><m:den>{R("2")}</m:den></m:f>");

    [Fact]
    public void Mixed_slot_keeps_box() =>
        Omml(@"\frac{a\placeholder{}}{2}", $"<m:f><m:num>{R("a")}<m:box><m:e></m:e></m:box></m:num><m:den>{R("2")}</m:den></m:f>");

    [Fact]
    public void Sup_slot_is_empty() =>
        Omml(@"x^{\placeholder{}}", $"<m:sSup><m:e>{R("x")}</m:e><m:sup></m:sup></m:sSup>");

    [Fact]
    public void Degree_slot_not_hidden() =>
        Omml(@"\sqrt[\placeholder{}]{x}", $"<m:rad><m:deg></m:deg><m:e>{R("x")}</m:e></m:rad>");

    [Fact]
    public void Matrix_cells_are_empty() =>
        Omml(@"\begin{pmatrix}\placeholder{} & \placeholder{}\\ \placeholder{} & \placeholder{}\end{pmatrix}",
            "<m:d><m:e><m:m><m:mr><m:e></m:e><m:e></m:e></m:mr><m:mr><m:e></m:e><m:e></m:e></m:mr></m:m></m:e></m:d>");

    [Fact]
    public void Equation_row_is_empty() =>
        Omml(@"\begin{gathered}\placeholder{}\\b\end{gathered}",
            $"<m:eqArr><m:e></m:e><m:e>{R("b")}</m:e></m:eqArr>");

    [Fact]
    public void Id_is_dropped_with_lossy()
    {
        var result = FormulaConverter.LatexToOmml(@"\placeholder[a]{}");
        Assert.True(SameXml(OMath("<m:box><m:e></m:e></m:box>"), result.Output));
        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("lossy", diag.Code);
    }

    [Fact]
    public void Content_is_kept_with_lossy()
    {
        var result = FormulaConverter.LatexToOmml(@"\placeholder{x}");
        Assert.True(SameXml(OMath(R("x")), result.Output));
        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("lossy", diag.Code);
    }

    [Fact]
    public void Id_and_content_warn_once()
    {
        var result = FormulaConverter.LatexToOmml(@"\placeholder[a]{x}");
        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("lossy", diag.Code);
    }

    [Theory]
    [InlineData(@"\placeholder")]
    [InlineData(@"\placeholder[a")]
    [InlineData(@"\placeholder{")]
    public void Malformed_throws(string latex)
    {
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
    }

    [Theory]
    [InlineData(@"\text{\placeholder{}}")]
    [InlineData(@"\operatorname{\placeholder{}}")]
    public void Raw_text_contexts_do_not_crash(string latex)
    {
        var ex = Record.Exception(() => FormulaConverter.LatexToOmml(latex));
        Assert.True(ex is null or FormulaParseException, $"Expected null or FormulaParseException, got {ex?.GetType().Name}: {ex?.Message}");
    }

    [Fact]
    public void Writer_prints_placeholder()
    {
        Assert.Equal(@"\placeholder{}", LatexWriter.Write(new Placeholder()));
        Assert.Equal(@"x^{\placeholder{}}", LatexWriter.Write(new Script(new Run("x", new RunStyle()), null, new Placeholder())));
        Assert.Equal(@"\placeholder{}^2", LatexWriter.Write(new Script(new Placeholder(), null, new Run("2", new RunStyle()))));
    }
}
