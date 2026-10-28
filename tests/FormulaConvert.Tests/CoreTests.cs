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
        Assert.Equal("x", result.Output);
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
}
