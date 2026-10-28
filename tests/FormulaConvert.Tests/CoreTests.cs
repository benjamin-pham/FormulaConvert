using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class CoreTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    [Fact] public void Letters_merge_into_one_run() => Check("a+b=1", R("a+b=1"));
    [Fact] public void Spaces_ignored() => Check("a + b", R("a+b"), "a+b");
    [Fact] public void Greek_symbol() => Check("\\alpha x", R("αx"));
    [Fact] public void Group_is_transparent() => Check("{a}b", R("ab"), "ab");
    [Fact] public void Empty_input() => Check("", "");
    [Fact] public void Empty_group() => Check("{}", "", "");
    [Fact] public void Unicode_command() => Check("\\unicode{\"2B1A}", R("⬚"), "⬚");

    [Theory]
    [InlineData("\\char\"2A17", "⨗")]
    [InlineData("^^41", "A")]
    [InlineData("^^^^2a17", "⨗")]
    public void Char_and_caret_forms(string latex, string text) => Check(latex, R(text), text);

    [Fact] public void Escaped_specials() => Check("\\#\\$\\%\\&\\_\\{\\}", R("#$%&_{}"), "\\#\\$\\%\\&\\_\\{\\}");
    // U+2016 has no entry in the symbol table, so the canonical output is the character itself.
    [Fact] public void Double_bar_command() => Check("a\\|b", R("a\u2016b"), "a\u2016b");
    [Fact] public void Double_bar_alone() => Check("\\|", R("\u2016"), "\u2016");
    [Fact] public void Backslash_symbol() => Check("\\backslash", R("\\"));
    [Fact] public void Non_bmp_passthrough() => Check("𝐀+é中", R("𝐀+é中"));

    [Fact]
    public void Unknown_command_throws()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\foo"));
        Assert.Equal(0, ex.Position);
        Assert.Contains("\\foo", ex.Message);
    }

    [Theory]
    [InlineData("{a", 0)]   // the unclosed brace
    [InlineData("a}", 1)]   // the stray brace
    [InlineData("x{a{b}", 1)]
    public void Unbalanced_brace_throws(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Reader_ignores_word_noise()
    {
        var xml = $"""
            <m:oMathPara xmlns:m="{MathNamespace}" xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <m:oMath>
                <m:r>
                  <w:rPr/>
                  <m:rPr><m:sty m:val="p"/></m:rPr>
                  <m:t xml:space="preserve">x</m:t>
                </m:r>
                <m:ctrlPr/>
              </m:oMath>
            </m:oMathPara>
            """;
        var result = FormulaConverter.OmmlToLatex(xml);
        Assert.Equal("\\mathrm{x}", result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Unknown_omml_element_warns()
    {
        var result = FormulaConverter.OmmlToLatex(OMath("<m:foo>" + R("a") + "</m:foo>"));
        Assert.Equal("a", result.Output);
        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("unsupported-omml", d.Code);
    }

    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>An m:oMath that also declares the w namespace.</summary>
    private static string WOMath(string inner) =>
        $"<m:oMath xmlns:m=\"{MathNamespace}\" xmlns:w=\"{W}\">{inner}</m:oMath>";

    [Theory]
    [InlineData("<w:ins w:id=\"1\" w:author=\"A\">{0}</w:ins>")]
    [InlineData("<w:moveTo w:id=\"1\" w:author=\"A\">{0}</w:moveTo>")]
    [InlineData("<w:smartTag w:element=\"x\">{0}</w:smartTag>")]
    [InlineData("<w:customXml w:element=\"x\"><w:customXmlPr/>{0}</w:customXml>")]
    [InlineData("<w:sdt><w:sdtPr><w:alias w:val=\"x\"/></w:sdtPr><w:sdtContent>{0}</w:sdtContent></w:sdt>")]
    [InlineData("<w:ins w:id=\"1\" w:author=\"A\"><w:smartTag w:element=\"x\">{0}</w:smartTag></w:ins>")]
    public void Word_wrappers_are_transparent(string wrapper)
    {
        var result = FormulaConverter.OmmlToLatex(WOMath(R("a+") + string.Format(wrapper, R("b"))));
        Assert.Equal("a+b", result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("<w:del w:id=\"1\" w:author=\"A\">{0}</w:del>")]
    [InlineData("<w:moveFrom w:id=\"1\" w:author=\"A\">{0}</w:moveFrom>")]
    public void Deleted_content_is_skipped(string wrapper)
    {
        var result = FormulaConverter.OmmlToLatex(WOMath(R("a+") + string.Format(wrapper, R("b"))));
        Assert.Equal("a+", result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Tracked_insert_inside_structure()
    {
        var xml = WOMath($"<m:f><m:num><w:ins w:id=\"1\" w:author=\"A\">{R("x")}</w:ins></m:num><m:den>{R("y")}</m:den></m:f>");
        Assert.Equal("\\frac{x}{y}", FormulaConverter.OmmlToLatex(xml).Output);
    }

    [Fact]
    public void Unknown_foreign_wrapper_with_math_warns()
    {
        var result = FormulaConverter.OmmlToLatex(WOMath(R("a") + $"<x:wrap xmlns:x=\"urn:x\">{R("b")}</x:wrap>"));
        Assert.Equal("a", result.Output);
        var d = Assert.Single(result.Diagnostics);
        Assert.Equal("unsupported-omml", d.Code);
        Assert.Contains("wrap", d.Message);
    }

    [Fact]
    public void Foreign_element_without_math_is_ignored_silently()
    {
        var result = FormulaConverter.OmmlToLatex(WOMath(R("a") + "<w:bookmarkStart w:id=\"0\" w:name=\"x\"/><w:proofErr w:type=\"spellStart\"/>"));
        Assert.Equal("a", result.Output);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Multiple_omath_joined_by_newline()
    {
        var xml = $"<doc xmlns:m=\"{MathNamespace}\"><p><m:oMath>{R("a")}</m:oMath></p><p><m:oMath>{R("\\")}</m:oMath></p></doc>";
        Assert.Equal("a\n\\backslash", FormulaConverter.OmmlToLatex(xml).Output);
    }

    [Theory]
    [InlineData("<m:oMath")]
    [InlineData("not xml")]
    [InlineData("<doc/>")]
    [InlineData("<!DOCTYPE m:oMath [<!ENTITY x \"a\">]><m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"><m:r><m:t>&x;</m:t></m:r></m:oMath>")]
    [InlineData("<!DOCTYPE m:oMath SYSTEM \"file:///etc/passwd\"><m:oMath xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"/>")]
    public void Malformed_xml_throws(string xml)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.OmmlToLatex(xml));
        Assert.Equal(-1, ex.Position);
    }

    [Fact]
    public void Output_is_unindented_omath_string()
    {
        var output = FormulaConverter.LatexToOmml("a").Output;
        Assert.Equal(OMath(R("a")), output);
        Assert.Equal(OMath(""), FormulaConverter.LatexToOmml("").Output);
    }

    [Fact]
    public void Comments_are_ignored() => Check("a%comment\n+b", R("a+b"), "a+b");

    [Fact]
    public void Alias_expands() => Check("\\neq", R("≠"), "\\ne");

    [Fact]
    public void Command_followed_by_letter_gets_space() => Check("\\alpha\\beta", R("αβ"));

    [Fact]
    public void Escaped_specials_survive_word_text()
    {
        Assert.Equal("a\\&\\#\\{", FormulaConverter.OmmlToLatex(OMath(R("a&#{"))).Output);
    }

    [Theory]
    [InlineData("\\unicode{65}")]
    [InlineData("\\unicode{x41}")]
    [InlineData("\\unicode{\"41}")]
    [InlineData("\\char65")]
    [InlineData("\\char'101")]
    public void Character_code_forms(string latex) => Check(latex, R("A"), "A");

    [Fact] public void Brackets_are_characters() => Check("[a]", R("[a]"));

    [Theory]
    [InlineData("a\\", 1)]
    [InlineData("\\unicode{zz}", 8)]
    [InlineData("\\unicode x", 9)]
    [InlineData("a^", 1)]
    [InlineData("\\neq}", 4)]
    public void Malformed_latex_throws_with_position(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(position, ex.Position);
    }

    [Theory]
    [InlineData("\\unicode{0}", 8)]
    [InlineData("\\unicode{\"FFFE}", 8)]
    [InlineData("\\unicode{x1F}", 8)]
    [InlineData("\\unicode{\"D800}", 8)]
    [InlineData("a^^00", 1)]
    [InlineData("^^01", 0)]
    [InlineData("^^^^fffe", 0)]
    [InlineData("\\char\"FFFE", 5)]
    [InlineData("\\char\"1F", 5)]
    [InlineData("\\char0", 5)]
    [InlineData("a\u0001", 1)]
    public void Characters_xml_cannot_hold_throw(string latex, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Equal(position, ex.Position);
    }

    [Fact]
    public void Unpaired_surrogates_throw()
    {
        // Built in code: xUnit's InlineData serialization replaces lone surrogates with U+FFFD.
        foreach (var (latex, position) in new[] { ("a\uD800b", 1), ("\uDC00", 0), ("x\uDC00\uD800", 1), ("\\\uD800", 1), ("a\uD800", 1) })
        {
            var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
            Assert.True(position == ex.Position, $"position for U+{(int)latex[position]:X4} case: expected {position}, got {ex.Position}");
            Assert.Contains("surrogate", ex.Message);
        }
    }

    [Theory]
    [InlineData("^^09")]
    [InlineData("\\unicode{\"E000}")]
    [InlineData("\\unicode{\"10FFFF}")]
    public void Xml_allowed_edge_characters_parse(string latex) =>
        Assert.StartsWith("<m:oMath", FormulaConverter.LatexToOmml(latex).Output);

    [Fact]
    public void Deep_latex_nesting_throws()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(new string('{', 100000)));
        Assert.Equal(NestingGuard.LatexLimit, ex.Position);
        Assert.Contains("nesting too deep", ex.Message);
    }

    [Fact]
    public void Nesting_up_to_limit_is_accepted()
    {
        var latex = new string('{', NestingGuard.LatexLimit) + "a" + new string('}', NestingGuard.LatexLimit);
        Assert.Equal(OMath(R("a")), FormulaConverter.LatexToOmml(latex).Output);
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("{" + latex + "}"));
    }

    [Fact]
    public void Deep_omml_nesting_throws()
    {
        var inner = string.Concat(Enumerable.Repeat("<m:foo>", 5000)) + R("a") + string.Concat(Enumerable.Repeat("</m:foo>", 5000));
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.OmmlToLatex(OMath(inner)));
        Assert.Equal(-1, ex.Position);
        Assert.Contains("nesting too deep", ex.Message);
    }
}
