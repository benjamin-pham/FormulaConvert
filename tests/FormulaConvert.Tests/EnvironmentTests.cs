using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class EnvironmentTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    /// <summary>m:m; m:mcs only when some column is not centered (one m:mc per column, count 1).</summary>
    private static string Mx(string[][] rows, string? columns = null)
    {
        var mcs = columns == null ? "" : "<m:mPr><m:mcs>" + string.Concat(columns.Select(c =>
            $"<m:mc><m:mcPr><m:count m:val=\"1\"/><m:mcJc m:val=\"{(c == 'l' ? "left" : c == 'r' ? "right" : "center")}\"/></m:mcPr></m:mc>")) + "</m:mcs></m:mPr>";
        return "<m:m>" + mcs + string.Concat(rows.Select(r => "<m:mr>" + string.Concat(r.Select(c => $"<m:e>{c}</m:e>")) + "</m:mr>")) + "</m:m>";
    }

    private static string EqA(params string[] rows) => "<m:eqArr>" + string.Concat(rows.Select(r => $"<m:e>{r}</m:e>")) + "</m:eqArr>";

    private static string Dl(string open, string close, string inner)
    {
        var props = (open == "(" ? "" : $"<m:begChr m:val=\"{open}\"/>") + (close == ")" ? "" : $"<m:endChr m:val=\"{close}\"/>");
        return "<m:d>" + (props.Length == 0 ? "" : $"<m:dPr>{props}</m:dPr>") + $"<m:e>{inner}</m:e></m:d>";
    }

    private static readonly string AbCd = Mx([[R("a"), R("b")], [R("c"), R("d")]]);

    [Fact] public void Matrix() => Check("\\begin{matrix}a&b\\\\c&d\\end{matrix}", AbCd);
    [Fact] public void Matrix_spaced() => Check("\\begin{matrix} a & b \\\\ c & d \\end{matrix}", AbCd, "\\begin{matrix}a&b\\\\c&d\\end{matrix}");

    [Theory]
    [InlineData("pmatrix", "(", ")")]
    [InlineData("bmatrix", "[", "]")]
    [InlineData("Bmatrix", "{", "}")]
    [InlineData("vmatrix", "|", "|")]
    [InlineData("Vmatrix", "‖", "‖")]
    public void Bracketed(string name, string open, string close) =>
        Check($"\\begin{{{name}}}a&b\\\\c&d\\end{{{name}}}", Dl(open, close, AbCd));

    [Fact]
    public void Left_right_around_matrix_is_pmatrix() =>
        Check("\\left(\\begin{matrix}a&b\\\\c&d\\end{matrix}\\right)", Dl("(", ")", AbCd), "\\begin{pmatrix}a&b\\\\c&d\\end{pmatrix}");

    [Fact]
    public void Other_brackets_around_matrix_stay_left_right() =>
        Check("\\left\\langle\\begin{matrix}a\\end{matrix}\\right\\rangle", Dl("⟨", "⟩", Mx([[R("a")]])));

    [Fact]
    public void Smallmatrix_is_lossy()
    {
        Check("\\begin{smallmatrix}a&b\\end{smallmatrix}", Mx([[R("a"), R("b")]]), "\\begin{matrix}a&b\\end{matrix}");
        Assert.Equal("lossy", Assert.Single(FormulaConverter.LatexToOmml("\\begin{smallmatrix}a&b\\end{smallmatrix}").Diagnostics).Code);
    }

    [Fact]
    public void Array_columns() =>
        Check("\\begin{array}{lc}a&b\\\\c&d\\end{array}", Mx([[R("a"), R("b")], [R("c"), R("d")]], "l"));

    [Fact]
    public void Array_all_centered_is_matrix() =>
        Check("\\begin{array}{cc}a&b\\end{array}", Mx([[R("a"), R("b")]]), "\\begin{matrix}a&b\\end{matrix}");

    [Fact]
    public void Array_right_then_center_then_left() =>
        Check("\\begin{array}{rcl}a&b&c\\end{array}", Mx([[R("a"), R("b"), R("c")]], "rcl"));

    [Fact]
    public void Array_vertical_rules_are_lossy()
    {
        Check("\\begin{array}{l|c}a&b\\\\c&d\\end{array}", Mx([[R("a"), R("b")], [R("c"), R("d")]], "l"), "\\begin{array}{lc}a&b\\\\c&d\\end{array}");
        Assert.Equal("lossy", Assert.Single(FormulaConverter.LatexToOmml("\\begin{array}{l||c}a&b\\end{array}").Diagnostics).Code);
    }

    [Fact]
    public void Array_unknown_column_specifier_fails() =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\begin{array}{p}a\\end{array}"));

    [Fact]
    public void Starred_matrix_with_alignment()
    {
        Check("\\begin{matrix*}[l|r]a&b\\end{matrix*}", Mx([[R("a"), R("b")]], "lr"), "\\begin{array}{lr}a&b\\end{array}");
        Assert.Equal("lossy", Assert.Single(FormulaConverter.LatexToOmml("\\begin{matrix*}[l|r]a&b\\end{matrix*}").Diagnostics).Code);
    }

    [Fact]
    public void Starred_pmatrix_without_option() =>
        Check("\\begin{pmatrix*}a&b\\end{pmatrix*}", Dl("(", ")", Mx([[R("a"), R("b")]])), "\\begin{pmatrix}a&b\\end{pmatrix}");

    [Fact]
    public void Aligned_pmatrix_uses_array_inside_left_right() =>
        Check("\\begin{pmatrix*}[r]a&b\\end{pmatrix*}", Dl("(", ")", Mx([[R("a"), R("b")]], "r")), "\\left(\\begin{array}{rc}a&b\\end{array}\\right)");

    [Fact]
    public void Cases() =>
        Check("\\begin{cases}1&x>0\\\\0&x\\le0\\end{cases}",
            Dl("{", "", EqA(R("1&x>0"), R("0&x≤0"))), "\\begin{cases}1&x>0\\\\0&x\\leq0\\end{cases}");

    [Fact]
    public void Rcases() =>
        Check("\\begin{rcases}a&b\\\\c&d\\end{rcases}", Dl("", "}", EqA(R("a&b"), R("c&d"))));

    [Fact]
    public void Dcases_is_lossy()
    {
        Check("\\begin{dcases}a\\\\b\\end{dcases}", Dl("{", "", EqA(R("a"), R("b"))), "\\begin{cases}a\\\\b\\end{cases}");
        Assert.Equal("lossy", Assert.Single(FormulaConverter.LatexToOmml("\\begin{dcases}a\\\\b\\end{dcases}").Diagnostics).Code);
    }

    [Fact] public void Align() => Check("\\begin{align}f&=a\\\\&=b\\end{align}", EqA(R("f&=a"), R("&=b")));

    [Theory]
    [InlineData("aligned")]
    [InlineData("split")]
    [InlineData("align*")]
    public void Align_family(string name) =>
        Check($"\\begin{{{name}}}f&=a\\\\&=b\\end{{{name}}}", EqA(R("f&=a"), R("&=b")), "\\begin{align}f&=a\\\\&=b\\end{align}");

    [Fact] public void Gathered() => Check("\\begin{gathered}a\\\\b\\end{gathered}", EqA(R("a"), R("b")));
    [Fact] public void Gather() => Check("\\begin{gather}a\\\\b\\end{gather}", EqA(R("a"), R("b")), "\\begin{gathered}a\\\\b\\end{gathered}");

    [Fact]
    public void Multline_is_lossy()
    {
        Check("\\begin{multline}a\\\\b\\end{multline}", EqA(R("a"), R("b")), "\\begin{gathered}a\\\\b\\end{gathered}");
        Assert.Equal("lossy", Assert.Single(FormulaConverter.LatexToOmml("\\begin{multline}a\\\\b\\end{multline}").Diagnostics).Code);
    }

    [Theory]
    [InlineData("equation")]
    [InlineData("math")]
    [InlineData("displaymath")]
    [InlineData("subequations")]
    [InlineData("eqnarray")]
    [InlineData("center")]
    [InlineData("equation*")]
    public void Transparent(string name) => Check($"\\begin{{{name}}}x\\end{{{name}}}", R("x"), "x");

    [Fact]
    public void Short_row_is_padded() =>
        Check("\\begin{matrix}a&b\\\\c\\end{matrix}", Mx([[R("a"), R("b")], [R("c"), ""]]), "\\begin{matrix}a&b\\\\c&\\end{matrix}");

    [Fact]
    public void Trailing_row_break_is_dropped() =>
        Check("\\begin{matrix}a\\\\\\end{matrix}", Mx([[R("a")]]), "\\begin{matrix}a\\end{matrix}");

    [Fact] public void Empty_cells() => Check("\\begin{matrix}&a\\\\b&\\end{matrix}", Mx([["", R("a")], [R("b"), ""]]));
    [Fact] public void Empty_body() => Check("\\begin{matrix}\\end{matrix}", "<m:m></m:m>");
    [Fact] public void Empty_pmatrix_body() => Check("\\begin{pmatrix}\\end{pmatrix}", Dl("(", ")", "<m:m></m:m>"));
    [Fact] public void Empty_align_body() => Check("\\begin{gathered}\\end{gathered}", "<m:eqArr></m:eqArr>");

    [Fact]
    public void Cell_content_is_full_math() =>
        Check("\\begin{matrix}\\frac12&x^2\\end{matrix}",
            Mx([["<m:f><m:num>" + R("1") + "</m:num><m:den>" + R("2") + "</m:den></m:f>", "<m:sSup><m:e>" + R("x") + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>"]]),
            "\\begin{matrix}\\frac{1}{2}&x^2\\end{matrix}");

    [Fact]
    public void Nested_matrix() =>
        Check("\\begin{matrix}\\begin{matrix}a&b\\end{matrix}&c\\end{matrix}", Mx([[Mx([[R("a"), R("b")]]), R("c")]]));

    [Fact]
    public void Escaped_ampersand_in_cell_round_trips() =>
        Check("\\begin{matrix}\\&&a\\end{matrix}", Mx([[R("&"), R("a")]]));

    [Fact]
    public void Matrix_inside_align_row() =>
        Check("\\begin{align}a&=\\begin{pmatrix}1\\\\2\\end{pmatrix}\\end{align}",
            EqA(R("a&=") + Dl("(", ")", Mx([[R("1")], [R("2")]]))));

    [Theory]
    [InlineData("\\begin{matrix}a&b\\end{pmatrix}", "matrix", "pmatrix")]
    [InlineData("\\begin{cases}a\\end{align}", "cases", "align")]
    public void Mismatched_end_names_both(string latex, string expected, string found)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Contains(expected, ex.Message);
        Assert.Contains(found, ex.Message);
        Assert.Equal(latex.IndexOf("\\end", StringComparison.Ordinal), ex.Position);
    }

    [Theory]
    [InlineData("x+\\begin{matrix}a&b")]
    [InlineData("x+\\begin{matrix}a&b}")]
    [InlineData("x+\\begin{matrix}a&b\\\\")]
    public void Missing_end_is_positioned_at_begin(string latex)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(2, ex.Position);
        Assert.Contains("\\end{matrix}", ex.Message);
    }

    [Fact]
    public void Unknown_environment_is_positioned_at_begin()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("x\\begin{foo}x\\end{foo}"));
        Assert.Equal(1, ex.Position);
        Assert.Contains("foo", ex.Message);
    }

    [Theory]
    [InlineData("a\\end{matrix}", 1)]
    [InlineData("a&b", 1)]
    [InlineData("a\\\\b", 1)]
    [InlineData("{\\begin{matrix}a\\end{matrix}\\end{matrix}}", 28)]
    [InlineData("\\begin{matrix}\\frac a&b\\end{matrix}", -1)]
    [InlineData("\\begin{matrix}a\\right)\\end{matrix}", -1)]
    [InlineData("\\begin{align}{a&b}\\end{align}", -1)]
    [InlineData("\\begin{equation}a&b\\end{equation}", -1)]
    [InlineData("\\begin{matrix}a", 0)]
    [InlineData("\\begin matrix", -1)]
    public void Stray_markers_fail(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        if (position >= 0) Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Deep_nesting_is_an_error_not_a_crash()
    {
        var latex = string.Concat(Enumerable.Repeat("\\begin{matrix}", 5000)) + "a" + string.Concat(Enumerable.Repeat("\\end{matrix}", 5000));
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
    }

    [Fact]
    public void Deep_omml_nesting_is_an_error_not_a_crash()
    {
        var open = string.Concat(Enumerable.Repeat("<m:m><m:mr><m:e>", 5000));
        var close = string.Concat(Enumerable.Repeat("</m:e></m:mr></m:m>", 5000));
        Assert.ThrowsAny<FormulaParseException>(() => FormulaConverter.OmmlToLatex(OMath(open + close)));
    }

    [Fact]
    public void Omml_matrix_with_noise_and_ragged_rows()
    {
        var omml = OMath("<m:m><m:mPr><m:plcHide m:val=\"1\"/><m:mcs><m:mc><m:mcPr><m:count m:val=\"2\"/><m:mcJc m:val=\"right\"/></m:mcPr></m:mc></m:mcs></m:mPr>"
            + "<m:mr><m:e>" + R("a") + "</m:e><m:e>" + R("b") + "</m:e></m:mr><m:mr><m:e><m:ctrlPr/></m:e></m:mr></m:m>");
        Assert.Equal("\\begin{array}{rr}a&b\\\\\\end{array}", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Omml_eqarr_without_marks_is_gathered() =>
        Assert.Equal("\\begin{gathered}a\\\\b\\end{gathered}", FormulaConverter.OmmlToLatex(OMath(EqA(R("a"), R("b")))).Output);
}
