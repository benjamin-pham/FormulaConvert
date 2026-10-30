using FormulaConvert.Ast;
using FormulaConvert.Latex;
using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class PlaceholderTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    [Fact]
    public void Alone_is_empty_box() =>
        Check(@"\placeholder{}", "<m:box><m:e></m:e></m:box>");

    [Fact]
    public void In_row_is_empty_box() =>
        Check(@"a+\placeholder{}", $"{R("a+")}<m:box><m:e></m:e></m:box>");

    [Fact]
    public void Fraction_slot_is_empty() =>
        Check(@"\frac{\placeholder{}}{2}", $"<m:f><m:num></m:num><m:den>{R("2")}</m:den></m:f>");

    [Fact]
    public void Mixed_slot_keeps_box() =>
        Check(@"\frac{a\placeholder{}}{2}", $"<m:f><m:num>{R("a")}<m:box><m:e></m:e></m:box></m:num><m:den>{R("2")}</m:den></m:f>");

    [Fact]
    public void Sup_slot_is_empty() =>
        Check(@"x^{\placeholder{}}", $"<m:sSup><m:e>{R("x")}</m:e><m:sup></m:sup></m:sSup>");

    [Fact]
    public void Degree_slot_not_hidden() =>
        Check(@"\sqrt[\placeholder{}]{x}", $"<m:rad><m:deg></m:deg><m:e>{R("x")}</m:e></m:rad>");

    [Fact]
    public void Matrix_cells_are_empty() =>
        Check(@"\begin{pmatrix}\placeholder{} & \placeholder{}\\ \placeholder{} & \placeholder{}\end{pmatrix}",
            "<m:d><m:e><m:m><m:mr><m:e></m:e><m:e></m:e></m:mr><m:mr><m:e></m:e><m:e></m:e></m:mr></m:m></m:e></m:d>",
            @"\begin{pmatrix}\placeholder{}&\placeholder{}\\\placeholder{}&\placeholder{}\end{pmatrix}");

    [Fact]
    public void Equation_row_is_empty() =>
        Check(@"\begin{gathered}\placeholder{}\\b\end{gathered}",
            $"<m:eqArr><m:e></m:e><m:e>{R("b")}</m:e></m:eqArr>");

    [Fact]
    public void Empty_frac_reads_placeholders() =>
        Check(@"\frac{}{}", "<m:f><m:num></m:num><m:den></m:den></m:f>", @"\frac{\placeholder{}}{\placeholder{}}");

    [Fact]
    public void Base_slot() =>
        Check(@"\placeholder{}^2", $"<m:sSup><m:e></m:e><m:sup>{R("2")}</m:sup></m:sSup>");

    [Fact]
    public void Sqrt_body() =>
        Check(@"\sqrt{\placeholder{}}", "<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg></m:deg><m:e></m:e></m:rad>");

    [Fact]
    public void Delimiter_body() =>
        Check(@"\left(\placeholder{}\right)", "<m:d><m:e></m:e></m:d>");

    [Fact]
    public void Function_arg_stays_empty() =>
        Check(@"\sin", $"<m:func><m:fName>{R("sin", "<m:sty m:val=\"p\"/>")}</m:fName><m:e></m:e></m:func>");

    [Fact]
    public void Function_arg_placeholder_is_dropped() =>
        Check(@"\sin\placeholder{}", $"<m:func><m:fName>{R("sin", "<m:sty m:val=\"p\"/>")}</m:fName><m:e></m:e></m:func>", @"\sin");

    [Fact]
    public void Nary_body_stays_empty() =>
        Check(@"\sum_{i=1}^n", $"<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub>{R("i=1")}</m:sub><m:sup>{R("n")}</m:sup><m:e></m:e></m:nary>");

    [Fact]
    public void Nary_body_placeholder_is_dropped() =>
        Check(@"\sum\placeholder{}", "<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:subHide m:val=\"1\"/><m:supHide m:val=\"1\"/></m:naryPr><m:sub></m:sub><m:sup></m:sup><m:e></m:e></m:nary>", @"\sum");

    [Fact]
    public void Hidden_degree_is_not_placeholder() =>
        Check(@"\sqrt{x}", $"<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg></m:deg><m:e>{R("x")}</m:e></m:rad>");

    [Fact]
    public void Prescript_empty_stays_absent() =>
        Check(@"{}_a x", $"<m:sPre><m:sub>{R("a")}</m:sub><m:sup></m:sup><m:e>{R("x")}</m:e></m:sPre>");

    [Fact]
    public void Sparse_matrix_cells() =>
        Check(@"\begin{pmatrix}1&\\&1\end{pmatrix}",
            $"<m:d><m:e><m:m><m:mr><m:e>{R("1")}</m:e><m:e></m:e></m:mr><m:mr><m:e></m:e><m:e>{R("1")}</m:e></m:mr></m:m></m:e></m:d>",
            @"\begin{pmatrix}1&\placeholder{}\\\placeholder{}&1\end{pmatrix}");

    [Fact]
    public void Ragged_matrix_padding() =>
        Check(@"\begin{matrix}a&b\\c\end{matrix}",
            $"<m:m><m:mr><m:e>{R("a")}</m:e><m:e>{R("b")}</m:e></m:mr><m:mr><m:e>{R("c")}</m:e><m:e></m:e></m:mr></m:m>",
            @"\begin{matrix}a&b\\c&\placeholder{}\end{matrix}");

    [Fact]
    public void Empty_base_reads_placeholder() =>
        Check(@"{}^2", $"<m:sSup><m:e></m:e><m:sup>{R("2")}</m:sup></m:sSup>", @"\placeholder{}^2");

    [Fact]
    public void Isotope_prescript_unchanged() =>
        Check(@"{}^{14}C", $"<m:sPre><m:sub></m:sub><m:sup>{R("14")}</m:sup><m:e>{R("C")}</m:e></m:sPre>");

    [Fact]
    public void Arrow_label_stays_empty() =>
        Check(@"\xrightarrow{}", $"<m:limUpp><m:e>{R("→")}</m:e><m:lim></m:lim></m:limUpp>", @"\overset{}{\rightarrow}");

    [Fact]
    public void Phantom_stays_empty() =>
        Check(@"\phantom{}", "<m:phant><m:phantPr><m:show m:val=\"0\"/></m:phantPr><m:e></m:e></m:phant>");

    [Fact]
    public void Function_name_stays_empty()
    {
        var omml = $"<m:func><m:fName></m:fName><m:e>{R("x")}</m:e></m:func>";
        var latex = FormulaConverter.OmmlToLatex(OMath(omml)).Output;
        Assert.DoesNotContain(@"\placeholder", latex);
    }

    [Fact]
    public void ReadOnly_empty_box_without_e_reads_placeholder()
    {
        var latex = FormulaConverter.OmmlToLatex(OMath("<m:box><m:boxPr/></m:box>")).Output;
        Assert.Equal(@"\placeholder{}", latex);
    }

    [Fact]
    public void ReadOnly_missing_den_reads_placeholder()
    {
        var latex = FormulaConverter.OmmlToLatex(OMath($"<m:f><m:num>{R("a")}</m:num></m:f>")).Output;
        Assert.Equal(@"\frac{a}{\placeholder{}}", latex);
    }

    [Fact]
    public void ReadOnly_ctrlPr_and_empty_run_reads_placeholder()
    {
        var omml = "<m:f><m:num><m:ctrlPr/></m:num><m:den><m:r><m:t></m:t></m:r></m:den></m:f>";
        var latex = FormulaConverter.OmmlToLatex(OMath(omml)).Output;
        Assert.Equal(@"\frac{\placeholder{}}{\placeholder{}}", latex);
    }

    [Fact]
    public void ReadOnly_prescript_empty_sub_has_no_placeholder()
    {
        var omml = $"<m:sPre><m:sub></m:sub><m:sup>{R("2")}</m:sup><m:e>{R("X")}</m:e></m:sPre>";
        var latex = FormulaConverter.OmmlToLatex(OMath(omml)).Output;
        Assert.Equal("{}^2 X", latex);
        Assert.DoesNotContain(@"\placeholder", latex);
    }

    [Fact]
    public void ReadOnly_empty_oMath_reads_empty_string()
    {
        var latex = FormulaConverter.OmmlToLatex(OMath("")).Output;
        Assert.Equal("", latex);
    }

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
