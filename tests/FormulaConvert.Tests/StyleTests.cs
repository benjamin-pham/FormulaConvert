using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class StyleTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static string Sty(string v) => $"<m:sty m:val=\"{v}\"/>";

    private static string Scr(string v) => $"<m:scr m:val=\"{v}\"/>";

    private const string Nor = "<m:nor/>";

    /// <summary>A normal-text run whose text contains whitespace (so <c>xml:space="preserve"</c> is expected).</summary>
    private static string NorSpaced(string text) =>
        $"<m:r><m:rPr><m:nor/></m:rPr><m:t xml:space=\"preserve\">{Escape(text)}</m:t></m:r>";

    [Theory]
    [InlineData("\\mathbf{x}", "b", "\\mathbf{x}")]
    [InlineData("\\boldsymbol{x}", "b", "\\mathbf{x}")]
    [InlineData("\\bm{x}", "b", "\\mathbf{x}")]
    [InlineData("\\bold{x}", "b", "\\mathbf{x}")]
    [InlineData("\\textbf{x}", "b", "\\mathbf{x}")]
    [InlineData("\\mathrm{x}", "p", "\\mathrm{x}")]
    [InlineData("\\mathbfit{x}", "bi", "\\mathbfit{x}")]
    public void Sty_commands(string latex, string val, string canonical) =>
        Check(latex, R("x", Sty(val)), canonical);

    [Theory]
    [InlineData("\\mathit{x}")]
    [InlineData("\\textit{x}")]
    public void Italic_is_the_default_style(string latex) => Check(latex, R("x"), "x");

    [Theory]
    [InlineData("\\mathbb{R}", "double-struck", "\\mathbb{R}")]
    [InlineData("\\Bbb{R}", "double-struck", "\\mathbb{R}")]
    [InlineData("\\mathcal{R}", "script", "\\mathcal{R}")]
    [InlineData("\\mathscr{R}", "script", "\\mathcal{R}")]
    [InlineData("\\mathfrak{R}", "fraktur", "\\mathfrak{R}")]
    [InlineData("\\mathsf{R}", "sans-serif", "\\mathsf{R}")]
    [InlineData("\\mathtt{R}", "monospace", "\\mathtt{R}")]
    [InlineData("\\texttt{R}", "monospace", "\\mathtt{R}")]
    public void Scr_commands(string latex, string val, string canonical) =>
        Check(latex, R("R", Scr(val)), canonical);

    [Fact]
    public void Nested_styles_combine() =>
        Check("\\mathbf{\\mathbb{R}}", R("R", Scr("double-struck") + Sty("b")));

    [Fact]
    public void Nested_styles_combine_in_either_order() =>
        Check("\\mathbb{\\mathbf{R}}", R("R", Scr("double-struck") + Sty("b")), "\\mathbf{\\mathbb{R}}");

    [Fact]
    public void Inner_style_wins_over_outer_one() =>
        Check("\\mathrm{a\\mathbf{b}}", R("a", Sty("p")) + R("b", Sty("b")), "\\mathrm{a}\\mathbf{b}");

    [Fact]
    public void Plain_with_script_style() =>
        Check("\\mathrm{\\mathbb{R}}", R("R", Scr("double-struck") + Sty("p")));

    [Fact]
    public void Style_covers_the_whole_argument() =>
        Check("\\mathbf{x+1}", R("x+1", Sty("b")));

    [Fact]
    public void Style_reaches_into_fractions() =>
        Check("\\mathbf{\\frac{a}{b}}",
            "<m:f><m:num>" + R("a", Sty("b")) + "</m:num><m:den>" + R("b", Sty("b")) + "</m:den></m:f>",
            "\\frac{\\mathbf{a}}{\\mathbf{b}}");

    [Fact]
    public void Style_reaches_into_scripts() =>
        Check("\\mathbf{x^2}",
            "<m:sSup><m:e>" + R("x", Sty("b")) + "</m:e><m:sup>" + R("2", Sty("b")) + "</m:sup></m:sSup>",
            "\\mathbf{x}^\\mathbf{2}");

    [Fact]
    public void Style_only_covers_its_argument() =>
        Check("a\\mathbf{b}c", R("a") + R("b", Sty("b")) + R("c"));

    [Fact]
    public void Style_command_with_single_atom_argument() =>
        Check("\\mathbf x y", R("x", Sty("b")) + R("y"), "\\mathbf{x}y");

    [Fact]
    public void Style_as_script_base() =>
        Check("\\mathbb{R}^n",
            "<m:sSup><m:e>" + R("R", Scr("double-struck")) + "</m:e><m:sup>" + R("n") + "</m:sup></m:sSup>");

    [Fact]
    public void Style_alias_expands() =>
        Check("\\R", R("R", Scr("double-struck")), "\\mathbb{R}");

    [Fact]
    public void Function_names_stay_commands() =>
        Check("\\sin x",
            "<m:func><m:fName>" + R("sin", Sty("p")) + "</m:fName><m:e>" + R("x") + "</m:e></m:func>");

    [Fact]
    public void Text_with_trailing_space() =>
        Check("\\text{if } x", NorSpaced("if ") + R("x"), "\\text{if }x");

    [Theory]
    [InlineData("\\text{a b}")]
    [InlineData("\\textrm{a b}")]
    [InlineData("\\textnormal{a b}")]
    [InlineData("\\mbox{a b}")]
    public void Text_commands_keep_spaces(string latex) =>
        Check(latex, NorSpaced("a b"), "\\text{a b}");

    [Fact]
    public void Text_unescapes_special_characters() =>
        Check("\\text{50\\%}", R("50%", Nor), "\\text{50\\%}");

    [Fact]
    public void Text_escapes_all_special_characters() =>
        Check("\\text{\\%\\&\\$\\#\\_\\{\\}}", R("%&$#_{}", Nor));

    [Fact]
    public void Text_keeps_nested_braces_balanced() =>
        Check("\\text{a{b}c}", R("a{b}c", Nor), "\\text{a\\{b\\}c}");

    [Fact]
    public void Text_backslash_round_trips() =>
        Check("\\text{a\\backslash b}", R("a\\b", Nor), "\\text{a\\backslash{}b}");

    [Fact]
    public void Text_non_ascii_passes_through() =>
        Check("\\text{é中}", R("é中", Nor));

    [Fact]
    public void Text_inside_a_style_stays_normal() =>
        Check("\\mathbf{\\text{ab}}", R("ab", Nor), "\\text{ab}");

    [Fact]
    public void Empty_text_is_empty() =>
        Check("a\\text{}b", R("ab"), "ab");

    [Fact]
    public void Text_as_script_argument() =>
        Check("x_\\text{ab}",
            "<m:sSub><m:e>" + R("x") + "</m:e><m:sub>" + R("ab", Nor) + "</m:sub></m:sSub>",
            "x_{\\text{ab}}");

    [Fact]
    public void Text_command_inside_text_is_kept_literally_with_a_warning()
    {
        var result = FormulaConverter.LatexToOmml("\\text{x \\alpha}");
        Assert.Contains("x \\alpha", result.Output);
        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("lossy", d.Code);
    }

    [Theory]
    [InlineData("\\text{unclosed")]
    [InlineData("\\text")]
    [InlineData("\\mathbf")]
    [InlineData("\\mathbf{x")]
    public void Unclosed_arguments_throw(string latex)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.True(ex.Position >= 0);
    }

    [Fact]
    public void Unclosed_text_is_positioned_at_the_brace()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("a\\text{unclosed"));
        Assert.Equal(6, ex.Position);
    }

    // ---- Reader ----

    private static string Read(string inner, out IReadOnlyList<Diagnostic> diagnostics)
    {
        var result = FormulaConverter.OmmlToLatex(OMath(inner));
        diagnostics = result.Diagnostics;
        return result.Output;
    }

    [Fact]
    public void Reader_combines_sty_and_scr()
    {
        Assert.Equal("\\mathbf{\\mathfrak{g}}", Read(R("g", Sty("b") + Scr("fraktur")), out var d));
        Assert.Empty(d);
    }

    [Fact]
    public void Reader_accepts_italic_sty()
    {
        Assert.Equal("x", Read(R("x", Sty("i")), out var d));
        Assert.Empty(d);
    }

    [Fact]
    public void Reader_ignores_unknown_sty_with_diagnostic()
    {
        Assert.Equal("x", Read(R("x", Sty("zz")), out var d));
        Assert.Equal("unsupported-omml", Assert.Single(d).Code);
    }

    [Fact]
    public void Reader_ignores_unknown_scr_with_diagnostic()
    {
        Assert.Equal("\\mathbf{x}", Read(R("x", Scr("zz") + Sty("b")), out var d));
        Assert.Equal("unsupported-omml", Assert.Single(d).Code);
    }

    [Fact]
    public void Reader_nor_wins_over_other_styles()
    {
        Assert.Equal("\\text{x}", Read(R("x", Nor + Scr("fraktur") + Sty("b")), out var d));
        Assert.Empty(d);
    }

    [Fact]
    public void Reader_nor_with_false_value_is_not_normal()
    {
        Assert.Equal("x", Read(R("x", "<m:nor m:val=\"0\"/>"), out _));
    }

    [Fact]
    public void Reader_reads_word_run_with_both_property_sets()
    {
        var xml = $"""
            <m:oMath xmlns:m="{MathNamespace}" xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <m:r>
                <m:rPr><m:scr m:val="double-struck"/><m:sty m:val="p"/></m:rPr>
                <w:rPr><w:rFonts w:ascii="Cambria Math" w:hAnsi="Cambria Math"/><w:b/></w:rPr>
                <m:t>R</m:t>
              </m:r>
            </m:oMath>
            """;
        var result = FormulaConverter.OmmlToLatex(xml);
        Assert.Equal("\\mathrm{\\mathbb{R}}", result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Reader_keeps_multi_character_styled_runs()
    {
        Assert.Equal("\\mathbf{abc}", Read(R("abc", Sty("b")), out _));
    }

    [Fact]
    public void Math_alphanumerics_pass_through()
    {
        Assert.Equal("\U0001D400", Read(R("\U0001D400"), out _));
    }

    [Fact]
    public void Writer_merges_equal_styles_and_marks_normal_text()
    {
        var omml = FormulaConverter.LatexToOmml("\\mathbf{ab}\\text{c d}").Output;
        Assert.True(SameXml(OMath(R("ab", Sty("b")) + NorSpaced("c d")), omml), omml);
    }

    [Fact]
    public void Writer_marks_leading_space_as_preserved()
    {
        var omml = FormulaConverter.LatexToOmml("\\text{ a}").Output;
        Assert.True(SameXml(OMath(NorSpaced(" a")), omml), omml);
    }
}
