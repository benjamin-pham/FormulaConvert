using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class ScriptTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static string SSup(string e, string sup) => $"<m:sSup><m:e>{e}</m:e><m:sup>{sup}</m:sup></m:sSup>";
    private static string SSub(string e, string sub) => $"<m:sSub><m:e>{e}</m:e><m:sub>{sub}</m:sub></m:sSub>";
    private static string SSubSup(string e, string sub, string sup) =>
        $"<m:sSubSup><m:e>{e}</m:e><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup></m:sSubSup>";
    private static string SPre(string sub, string sup, string e) =>
        $"<m:sPre><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup><m:e>{e}</m:e></m:sPre>";
    private static string Rad(string e, string? deg = null) =>
        "<m:rad>" + (deg == null ? "<m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg></m:deg>" : $"<m:deg>{deg}</m:deg>") + $"<m:e>{e}</m:e></m:rad>";

    [Fact] public void Sup() => Check("x^2", SSup(R("x"), R("2")));
    [Fact] public void Sub() => Check("x_i", SSub(R("x"), R("i")));
    [Fact] public void SubSup() => Check("x_i^2", SSubSup(R("x"), R("i"), R("2")));
    [Fact] public void SupSub_any_order() => Check("x^2_i", SSubSup(R("x"), R("i"), R("2")), "x_i^2");
    [Fact] public void Braced_sup() => Check("x^{ab}", SSup(R("x"), R("ab")));
    [Fact] public void Script_binds_to_last_atom() => Check("ab^2", R("a") + SSup(R("b"), R("2")));
    [Fact] public void Group_base() => Check("{ab}^2", SSup(R("ab"), R("2")));
    [Fact] public void Nested_sup() => Check("x^{2^3}", SSup(R("x"), SSup(R("2"), R("3"))));
    [Fact] public void Nested_script_base_is_braced() => Check("{x^2}^3", SSup(SSup(R("x"), R("2")), R("3")));
    [Fact] public void Symbol_command_sup() => Check("x^\\alpha", SSup(R("x"), R("α")));
    [Fact] public void Single_braced_char_loses_braces() => Check("x^{2}", SSup(R("x"), R("2")), "x^2");
    [Fact] public void Empty_sup_arg() => Check("x^{}", SSup(R("x"), ""));
    [Fact] public void Command_base() => Check("\\alpha^2", SSup(R("α"), R("2")), "\\alpha^2");

    [Fact] public void Space_after_unbraced_script() => Check("x^2 y", SSup(R("x"), R("2")) + R("y"), "x^2 y");
    [Fact] public void Space_after_unbraced_sub() => Check("x_i y", SSub(R("x"), R("i")) + R("y"), "x_i y");
    [Fact] public void Space_before_digit() => Check("x^2 3", SSup(R("x"), R("2")) + R("3"), "x^2 3");
    [Fact] public void No_space_after_braced_script() => Check("x^{ab}y", SSup(R("x"), R("ab")) + R("y"));
    [Fact] public void No_space_before_operator() => Check("x^2+y", SSup(R("x"), R("2")) + R("+y"));
    [Fact] public void Space_after_subsup() => Check("x_i^2 y", SSubSup(R("x"), R("i"), R("2")) + R("y"));

    [Fact] public void Prime() => Check("f'", SSup(R("f"), R("′")), "f^{\\prime}");
    [Fact] public void Double_prime() => Check("f''", SSup(R("f"), R("′′")), "f^{\\prime\\prime}");
    [Fact] public void Prime_command_sup() => Check("f^\\prime", SSup(R("f"), R("′")), "f^{\\prime}");
    [Fact] public void Prime_with_sub() => Check("f_1'", SSubSup(R("f"), R("1"), R("′")), "f_1^{\\prime}");
    [Fact] public void Prime_then_sub() => Check("f'_1", SSubSup(R("f"), R("1"), R("′")), "f_1^{\\prime}");
    [Fact] public void Prime_followed_by_text() => Check("f' x", SSup(R("f"), R("′")) + R("x"), "f^{\\prime}x");

    [Fact]
    public void Reader_double_prime_char_is_two_primes() =>
        Assert.Equal("f^{\\prime\\prime}", FormulaConverter.OmmlToLatex(OMath(SSup(R("f"), R("″")))).Output);

    [Fact] public void Prefix_script() => Check("{}_a^b x", SPre(R("a"), R("b"), R("x")));
    [Fact] public void Prefix_script_does_not_swallow_nonempty_group_script() =>
        Check("{}_a{xy}^2z", SPre(R("a"), "", SSup(R("xy"), R("2"))) + R("z"), "{}_a{xy}^2 z");
    [Fact] public void Prefix_script_whitespace_in_empty_group() => Check("{ }_a x", SPre(R("a"), "", R("x")), "{}_a x");
    [Fact] public void Prefix_script_sup_only() => Check("{}^b x", SPre("", R("b"), R("x")), "{}^b x");
    [Fact] public void Prefix_script_group() => Check("{}_a^b{xy}", SPre(R("a"), R("b"), R("xy")));
    [Fact] public void Prefix_script_sub_only() => Check("{}_a x", SPre(R("a"), "", R("x")), "{}_a x");
    [Fact] public void Empty_group_alone_is_empty() => Check("{}x", R("x"), "x");
    [Fact] public void Empty_group_then_end() => Check("{}", "", "");
    [Fact] public void Empty_group_with_script_then_end() => Check("{}^2", SSup("", R("2")), "^2");
    [Fact] public void No_base() => Check("^2", SSup("", R("2")));

    [Fact] public void Sqrt() => Check("\\sqrt{x}", Rad(R("x")));
    [Fact] public void Sqrt_single_atom() => Check("\\sqrt x", Rad(R("x")), "\\sqrt{x}");
    [Fact] public void Sqrt_degree() => Check("\\sqrt[3]{x}", Rad(R("x"), R("3")));
    [Fact] public void Sqrt_empty() => Check("\\sqrt{}", Rad(""));
    [Fact] public void Sqrt_with_sup() => Check("\\sqrt{x}^2", SSup(Rad(R("x")), R("2")));
    [Fact] public void Surd() => Check("\\surd", R("√"), "\\surd");

    [Fact]
    public void Reader_accepts_word_forms()
    {
        var hidden = "<m:rad><m:radPr><m:degHide/><m:ctrlPr/></m:radPr><m:deg>" + R("3") + "</m:deg><m:e>" + R("x") + "</m:e></m:rad>";
        Assert.Equal("\\sqrt{x}", FormulaConverter.OmmlToLatex(OMath(hidden)).Output);
        var noDeg = "<m:rad><m:e>" + R("x") + "</m:e></m:rad>";
        Assert.Equal("\\sqrt{x}", FormulaConverter.OmmlToLatex(OMath(noDeg)).Output);
        var full = "<m:sSup><m:sSupPr><m:ctrlPr/></m:sSupPr><m:e>" + R("x") + "</m:e><m:sup>" + R("2") + "</m:sup></m:sSup>";
        Assert.Equal("x^2", FormulaConverter.OmmlToLatex(OMath(full)).Output);
    }

    [Fact]
    public void Reader_prefix_script_with_empty_sub()
    {
        var latex = FormulaConverter.OmmlToLatex(OMath(SPre("", R("b"), R("x")))).Output;
        Assert.Equal("{}^b x", latex);
        Assert.True(SameXml(OMath(SPre("", R("b"), R("x"))), FormulaConverter.LatexToOmml(latex).Output));
    }

    [Theory]
    [InlineData("x^1^2", "double superscript", 3)]
    [InlineData("x_1_2", "double subscript", 3)]
    [InlineData("x''^2", "double superscript", 3)]
    [InlineData("x^2'", "double superscript", 3)]
    [InlineData("x^2_1^3", "double superscript", 5)]
    public void Double_script_throws(string latex, string message, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Contains(message, ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Theory]
    [InlineData("a^", "missing argument for ^", 1)]
    [InlineData("a_", "missing argument for _", 1)]
    [InlineData("{a^}", "missing argument for ^", 2)]
    [InlineData("x^{\\sqrt}", "missing argument", 8)]
    [InlineData("\\sqrt", "missing argument", 5)]
    public void Missing_argument_position(string latex, string message, int position)
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        Assert.Contains(message, ex.Message);
        Assert.Equal(position, ex.Position);
    }

    [Theory]
    [InlineData("\\sqrt")]
    [InlineData("\\sqrt[3]")]
    [InlineData("{\\sqrt}")]
    [InlineData("x^")]
    [InlineData("x_")]
    [InlineData("\\sqrt[3{x}")]
    public void Missing_argument_throws(string latex) =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));

    [Theory]
    [InlineData("a^b", "a\\char\"5E b")]
    [InlineData("a~b", "a\\char\"7E b")]
    [InlineData("a'b", "a\\char\"27 b")]
    [InlineData("^1", "\\char\"5E 1")]
    [InlineData("^+", "\\char\"5E+")]
    public void Literal_special_chars_round_trip(string text, string latex)
    {
        var omml = OMath(R(text));
        Assert.Equal(latex, FormulaConverter.OmmlToLatex(omml).Output);
        Assert.True(SameXml(omml, FormulaConverter.LatexToOmml(latex).Output));
    }

    [Fact]
    public void Literal_caret_as_script_argument_and_base()
    {
        var omml = OMath(SSup(R("^"), R("'")));
        var latex = FormulaConverter.OmmlToLatex(omml).Output;
        Assert.Equal("\\char\"5E^{\\char\"27}", latex);
        Assert.True(SameXml(omml, FormulaConverter.LatexToOmml(latex).Output));
    }

    [Fact]
    public void Deeply_nested_scripts_do_not_crash()
    {
        var latex = string.Concat(Enumerable.Repeat("{}_a", 5000)) + "x";
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(latex));
        var sup = string.Concat(Enumerable.Repeat("x^{", 5000)) + "y" + new string('}', 5000);
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml(sup));
    }
}
