using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class NaryFunctionTests
{
    private const string Plain = "<m:sty m:val=\"p\"/>";

    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static string SSup(string e, string sup) => $"<m:sSup><m:e>{e}</m:e><m:sup>{sup}</m:sup></m:sSup>";
    private static string SSubSup(string e, string sub, string sup) =>
        $"<m:sSubSup><m:e>{e}</m:e><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup></m:sSubSup>";
    private static string SSub(string e, string sub) => $"<m:sSub><m:e>{e}</m:e><m:sub>{sub}</m:sub></m:sSub>";

    /// <summary>m:nary; a null sub/sup is hidden (empty element plus subHide/supHide).</summary>
    private static string N(string chr, string? sub, string? sup, string e, string? limLoc = null) =>
        "<m:nary><m:naryPr>" + $"<m:chr m:val=\"{chr}\"/>"
        + (limLoc == null ? "" : $"<m:limLoc m:val=\"{limLoc}\"/>")
        + (sub == null ? "<m:subHide m:val=\"1\"/>" : "")
        + (sup == null ? "<m:supHide m:val=\"1\"/>" : "")
        + $"</m:naryPr><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup><m:e>{e}</m:e></m:nary>";

    private static string Fn(string name, string e) =>
        $"<m:func><m:fName>{name}</m:fName><m:e>{e}</m:e></m:func>";

    private static string FName(string name) => R(name, Plain);

    private static string LimLow(string e, string lim) =>
        $"<m:limLow><m:e>{e}</m:e><m:lim>{lim}</m:lim></m:limLow>";

    [Fact] public void Sum_with_bounds() => Check("\\sum_{i=0}^n a_i", N("∑", R("i=0"), R("n"), SSub(R("a"), R("i"))));
    [Fact] public void Int_default_placement() => Check("\\int_0^1 f", N("∫", R("0"), R("1"), R("f")));
    [Fact] public void Int_limits() =>
        Check("\\int_0^1\\limits f", N("∫", R("0"), R("1"), R("f"), "undOvr"), "\\int\\limits_0^1 f");
    [Fact] public void Sum_nolimits() =>
        Check("\\sum\\nolimits_a^b x", N("∑", R("a"), R("b"), R("x"), "subSup"), "\\sum\\nolimits_a^b x");
    [Fact] public void Sum_no_bounds() => Check("\\sum x", N("∑", null, null, R("x")));
    [Fact] public void Sum_braced_operand() =>
        Check("\\sum_{i}^{n}{a+b}", N("∑", R("i"), R("n"), R("a+b")), "\\sum_i^n{a+b}");
    [Fact] public void Prod_without_operand() => Check("\\prod", N("∏", null, null, ""));
    [Fact] public void Coprod() => Check("\\coprod_a x", N("∐", R("a"), null, R("x")));
    [Fact] public void Oint() => Check("\\oint_C f", N("∮", R("C"), null, R("f")));
    [Fact] public void Bigcup() => Check("\\bigcup_i A", N("⋃", R("i"), null, R("A")));
    [Fact] public void Bigoplus() => Check("\\bigoplus_i A", N("⨁", R("i"), null, R("A")));
    [Fact] public void Limits_equal_to_default_is_dropped() =>
        Check("\\sum\\limits_a x", N("∑", R("a"), null, R("x")), "\\sum_a x");
    [Fact] public void Nolimits_equal_to_default_for_int() =>
        Check("\\int\\nolimits_a x", N("∫", R("a"), null, R("x")), "\\int_a x");
    [Fact] public void Displaylimits_is_default() =>
        Check("\\int\\displaylimits_a x", N("∫", R("a"), null, R("x")), "\\int_a x");
    [Fact] public void Limits_between_scripts() =>
        Check("\\sum_a\\limits^b x", N("∑", R("a"), R("b"), R("x")), "\\sum_a^b x");
    [Fact] public void Sup_only() => Check("\\sum^n x", N("∑", null, R("n"), R("x")));
    [Fact] public void Operand_is_one_atom() => Check("\\sum_a xy", N("∑", R("a"), null, R("x")) + R("y"));
    [Fact] public void Operand_with_scripts() =>
        Check("\\sum x^2", N("∑", null, null, SSup(R("x"), R("2"))));
    [Fact] public void Operand_command() =>
        Check("\\sum_a \\alpha", N("∑", R("a"), null, R("α")), "\\sum_a\\alpha");
    [Fact] public void Nested_nary() =>
        Check("\\sum_a\\sum_b x", N("∑", R("a"), null, N("∑", R("b"), null, R("x"))));
    [Fact] public void Operand_stops_at_group_end() =>
        Check("{\\sum_a}", N("∑", R("a"), null, ""), "\\sum_a");
    [Fact] public void Double_sup_is_an_error() =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\sum^a^b x"));

    [Fact] public void Plain_run_coprod_stays_a_run() => Check("∐", R("∐"));
    [Fact] public void Plain_run_oint_stays_a_run() => Check("∮", R("∮"));
    [Fact] public void Plain_run_with_nary_char_inside() => Check("a∑b", R("a∑b"));
    [Fact] public void Plain_run_nary_char_does_not_swallow_next_atom() => Check("∐x", R("∐x"));

    [Fact] public void Sin() => Check("\\sin x", Fn(FName("sin"), R("x")));
    [Fact] public void Sin_parenthesized() => Check("\\sin(x+1)", Fn(FName("sin"), R("(x+1)")));
    [Fact] public void Sin_bracketed() => Check("\\sin[x]", Fn(FName("sin"), R("[x]")));
    [Fact] public void Sin_squared() =>
        Check("\\sin^2 x", Fn(SSup(FName("sin"), R("2")), R("x")));
    [Fact] public void Sin_takes_one_atom() => Check("\\sin xy", Fn(FName("sin"), R("x")) + R("y"));
    [Fact] public void Sin_paren_with_script_inside() =>
        Check("\\sin(x^2)", Fn(FName("sin"), R("(") + SSup(R("x"), R("2")) + R(")")), "\\sin{(x^2)}");
    [Fact] public void Sin_nested_parens() =>
        Check("\\sin((a)b)c", Fn(FName("sin"), R("((a)b)")) + R("c"));
    [Fact] public void Sin_two_parenthesized_groups_is_braced() =>
        Check("\\sin{(a)(b)}", Fn(FName("sin"), R("(a)(b)")));
    [Fact] public void Lim() =>
        Check("\\lim_{x\\to0} f", Fn(LimLow(FName("lim"), R("x→0")), R("f")), "\\lim_{x\\rightarrow0}f");
    [Fact] public void Max_sub_is_limit() =>
        Check("\\max_i x", Fn(LimLow(FName("max"), R("i")), R("x")));
    [Fact] public void Operatorname() =>
        Check("\\operatorname{argth}(\\theta)", Fn(FName("argth"), R("(θ)")));
    [Fact] public void Operatorname_with_table_name_writes_command() =>
        Check("\\operatorname{sin} x", Fn(FName("sin"), R("x")), "\\sin x");
    // A script whose base starts with ( must be braced, or the function would read a bracketed argument.
    [Fact] public void Function_operand_script_on_open_paren() =>
        Check("\\sin{(^2}", Fn(FName("sin"), SSup(R("("), R("2"))));
    [Fact] public void Function_operand_script_on_open_bracket() =>
        Check("\\sin{[^2}", Fn(FName("sin"), SSup(R("["), R("2"))));
    [Fact] public void Function_without_operand() => Check("\\sin", Fn(FName("sin"), ""));
    [Fact] public void Log_sub() => Check("\\log_2 8", Fn(SSub(FName("log"), R("2")), R("8")));
    [Fact] public void Lim_sup_is_script() =>
        Check("\\lim^2 f", Fn(SSup(FName("lim"), R("2")), R("f")));
    [Fact] public void Function_limits_modifier_is_ignored() =>
        Check("\\lim\\limits_a f", Fn(LimLow(FName("lim"), R("a")), R("f")), "\\lim_a f");
    [Fact] public void Function_in_script_base_is_braced() =>
        Check("{\\sin x}^2", SSup(Fn(FName("sin"), R("x")), R("2")));

    [Fact]
    public void Reader_nary_without_chr_is_integral()
    {
        var omml = OMath("<m:nary><m:naryPr><m:supHide m:val=\"1\"/></m:naryPr><m:sub>" + R("0") + "</m:sub><m:sup></m:sup><m:e>" + R("f") + "</m:e></m:nary>");
        Assert.Equal("\\int_0 f", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Reader_missing_limLoc_uses_character_default()
    {
        var omml = OMath("<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub>" + R("a") + "</m:sub><m:sup>" + R("b") + "</m:sup><m:e>" + R("x") + "</m:e></m:nary>");
        Assert.Equal("\\sum_a^b x", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Reader_function_name_from_several_runs()
    {
        var omml = OMath(Fn(R("a", Plain) + R("rg", Plain) + R("th", Plain), R("x")));
        Assert.Equal("\\operatorname{argth}x", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Reader_function_name_ignores_funcPr()
    {
        var omml = OMath("<m:func><m:funcPr><m:ctrlPr/></m:funcPr><m:fName>" + FName("cos") + "</m:fName><m:e>" + R("x") + "</m:e></m:func>");
        Assert.Equal("\\cos x", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Reader_nary_with_multi_char_operand_is_braced()
    {
        var omml = OMath(N("∑", null, null, R("ab")));
        Assert.Equal("\\sum{ab}", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact] public void Empty_function_operand_before_sibling() =>
        Check("\\sin{}x", Fn(FName("sin"), "") + R("x"));
    [Fact] public void Empty_function_operand_at_end_stays_bare() =>
        Check("\\sin", Fn(FName("sin"), ""));
    [Fact] public void Empty_nary_operand_before_sibling() =>
        Check("\\sum_a{}x", N("∑", R("a"), null, "") + R("x"));
    [Fact] public void Empty_nary_operand_at_end_stays_bare() =>
        Check("\\sum_a", N("∑", R("a"), null, ""));

    // \operatorname* places a subscript below the name, like \lim.
    [Fact] public void Starred_operatorname_is_a_limit() =>
        Check("\\operatorname*{argmax}_x f", Fn(LimLow(FName("argmax"), R("x")), R("f")));
    [Fact] public void Starred_operatorname_unknown_name() =>
        Check("\\operatorname*{foo}_x f", Fn(LimLow(FName("foo"), R("x")), R("f")));
    [Fact] public void Starred_operatorname_with_both_scripts_is_a_script() =>
        Check("\\operatorname*{foo}_x^y f", Fn(SSubSup(FName("foo"), R("x"), R("y")), R("f")), "\\operatorname{foo}_x^y f");
    [Fact] public void Starred_operatorname_without_scripts() =>
        Check("\\operatorname*{foo}x", Fn(FName("foo"), R("x")), "\\operatorname{foo}x");
    [Fact] public void Bounds_function_name_in_operatorname_star_writes_command() =>
        Check("\\operatorname*{max}_i x", Fn(LimLow(FName("max"), R("i")), R("x")), "\\max_i x");

    // The argument is math: commands become characters, never literal backslashes in m:t.
    [Fact] public void Operatorname_argument_symbols() =>
        Check("\\operatorname{\\alpha\\beta} x", Fn(FName("αβ"), R("x")), "\\operatorname{\\alpha\\beta}x");
    [Fact] public void Operatorname_argument_spacing_is_dropped()
    {
        var r = FormulaConverter.LatexToOmml("\\operatorname{arg\\,max} x");
        Assert.DoesNotContain("\\", r.Output);
        Assert.True(SameXml(OMath(Fn(FName("argmax"), R("x"))), r.Output), r.Output);
    }
    [Fact] public void Operatorname_ignores_spaces() =>
        Check("\\operatorname{ f o o } x", Fn(FName("foo"), R("x")), "\\operatorname{foo}x");
    [Fact] public void Operatorname_empty_throws() =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\operatorname{ }x"));

    // Names with LaTeX specials are escaped character by character.
    [Theory]
    [InlineData("a}b", "\\operatorname{a\\}b}x")]
    [InlineData("a%b", "\\operatorname{a\\%b}x")]
    [InlineData("a_b", "\\operatorname{a\\_b}x")]
    [InlineData("a\\b", "\\operatorname{a\\backslash b}x")]
    public void Reader_function_name_with_specials(string name, string latex)
    {
        Assert.Equal(latex, FormulaConverter.OmmlToLatex(OMath(Fn(FName(name), R("x")))).Output);
        Check(latex, Fn(FName(name), R("x")));
    }
    [Fact] public void Starred_operatorname_has_no_diagnostic() =>
        Assert.Empty(FormulaConverter.LatexToOmml("\\operatorname*{argmax}_x f").Diagnostics);

    [Theory]
    [InlineData("\\sech x", "sech")]
    [InlineData("\\csch x", "csch")]
    public void Hyperbolic_sech_and_csch(string latex, string name) =>
        Check(latex, Fn(FName(name), R("x")), $"\\operatorname{{{name}}}x");

    [Theory]
    [InlineData("\\intclockwise_C f", "∱")]
    [InlineData("\\varointclockwise_C f", "∲")]
    [InlineData("\\ointctrclockwise_C f", "∳")]
    public void Oriented_integrals_are_nary(string latex, string chr) =>
        Check(latex, N(chr, R("C"), null, R("f")));
}
