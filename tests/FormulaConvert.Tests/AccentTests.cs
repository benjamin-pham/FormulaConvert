using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class AccentTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static string Ac(string chr, string e) =>
        $"<m:acc><m:accPr><m:chr m:val=\"{chr}\"/></m:accPr><m:e>{e}</m:e></m:acc>";

    private static string Br(bool top, string e) =>
        $"<m:bar>{(top ? "" : "<m:barPr><m:pos m:val=\"bot\"/></m:barPr>")}<m:e>{e}</m:e></m:bar>";

    private static string Gc(string chr, bool top, string e) =>
        $"<m:groupChr><m:groupChrPr><m:chr m:val=\"{chr}\"/><m:pos m:val=\"{(top ? "top" : "bot")}\"/></m:groupChrPr><m:e>{e}</m:e></m:groupChr>";

    private static string LU(string e, string lim) => $"<m:limUpp><m:e>{e}</m:e><m:lim>{lim}</m:lim></m:limUpp>";

    private static string LL(string e, string lim) => $"<m:limLow><m:e>{e}</m:e><m:lim>{lim}</m:lim></m:limLow>";

    private static string SSup(string e, string sup) => $"<m:sSup><m:e>{e}</m:e><m:sup>{sup}</m:sup></m:sSup>";

    [Theory]
    [InlineData("\\hat", "̂")]
    [InlineData("\\tilde", "̃")]
    [InlineData("\\vec", "⃗")]
    [InlineData("\\dot", "̇")]
    [InlineData("\\ddot", "̈")]
    [InlineData("\\bar", "̄")]
    [InlineData("\\check", "̌")]
    [InlineData("\\breve", "̆")]
    [InlineData("\\mathring", "̊")]
    [InlineData("\\dddot", "⃛")]
    [InlineData("\\ddddot", "⃜")]
    [InlineData("\\acute", "́")]
    [InlineData("\\grave", "̀")]
    public void Accent_commands(string command, string chr) =>
        Check(command + "{x}", Ac(chr, R("x")));

    [Fact] public void Widehat() => Check("\\widehat{ABC}", Ac("̂", R("ABC")), "\\hat{ABC}");
    [Fact] public void Widetilde() => Check("\\widetilde{ABC}", Ac("̃", R("ABC")), "\\tilde{ABC}");
    [Fact] public void Widecheck() => Check("\\widecheck{ABC}", Ac("̌", R("ABC")), "\\check{ABC}");
    [Fact] public void Overrightarrow() => Check("\\overrightarrow{AB}", Ac("⃗", R("AB")), "\\vec{AB}");
    [Fact] public void Overleftarrow() => Check("\\overleftarrow{AB}", Ac("⃖", R("AB")));
    [Fact] public void Overleftrightarrow() => Check("\\overleftrightarrow{AB}", Ac("⃡", R("AB")));
    [Fact] public void Accent_single_atom_argument() => Check("\\hat x", Ac("̂", R("x")), "\\hat{x}");
    [Fact] public void Accent_then_script() => Check("\\hat{x}^2", SSup(Ac("̂", R("x")), R("2")));
    [Fact] public void Nested_accents() => Check("\\vec{\\hat{x}}", Ac("⃗", Ac("̂", R("x"))));
    [Fact] public void Accent_without_chr_defaults_to_hat() =>
        Assert.Equal("\\hat{x}", FormulaConverter.OmmlToLatex(OMath($"<m:acc><m:e>{R("x")}</m:e></m:acc>")).Output);
    [Fact] public void Accent_missing_argument_throws() =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\hat"));

    [Fact] public void Overline() => Check("\\overline{AB}", Br(true, R("AB")));
    [Fact] public void Underline() => Check("\\underline{AB}", Br(false, R("AB")));
    [Fact] public void Overbrace() => Check("\\overbrace{AB}", Gc("⏞", true, R("AB")));
    [Fact] public void Underbrace() => Check("\\underbrace{AB}", Gc("⏟", false, R("AB")));
    [Fact] public void Overparen() => Check("\\overparen{AB}", Gc("⏜", true, R("AB")));
    [Fact] public void Wideparen() => Check("\\wideparen{AB}", Gc("⏜", true, R("AB")), "\\overparen{AB}");
    [Fact] public void Underparen() => Check("\\underparen{AB}", Gc("⏝", false, R("AB")));
    [Fact] public void Underrightarrow() => Check("\\underrightarrow{AB}", Gc("→", false, R("AB")));
    [Fact] public void Underleftarrow() => Check("\\underleftarrow{AB}", Gc("←", false, R("AB")));
    [Fact] public void Underleftrightarrow() => Check("\\underleftrightarrow{AB}", Gc("↔", false, R("AB")));
    [Fact] public void Bar_with_explicit_top_position_reads_as_top() =>
        Assert.Equal("\\overline{x}",
            FormulaConverter.OmmlToLatex(OMath($"<m:bar><m:barPr><m:pos m:val=\"top\"/></m:barPr><m:e>{R("x")}</m:e></m:bar>")).Output);

    [Fact] public void Overset() => Check("\\overset{a}{x}", LU(R("x"), R("a")));
    [Fact] public void Stackrel() => Check("\\stackrel{a}{x}", LU(R("x"), R("a")), "\\overset{a}{x}");
    [Fact] public void Underset() => Check("\\underset{b}{x}", LL(R("x"), R("b")));
    [Fact] public void Overunderset() =>
        Check("\\overunderset{a}{b}{x}", LU(LL(R("x"), R("b")), R("a")));
    [Fact] public void Overset_of_underset_nesting() =>
        Check("\\overset{a}{\\underset{b}{x}}", LU(LL(R("x"), R("b")), R("a")), "\\overunderset{a}{b}{x}");
    [Fact] public void Underset_of_overset_nesting() =>
        Check("\\underset{b}{\\overset{a}{x}}", LL(LU(R("x"), R("a")), R("b")));
    [Fact] public void Overset_then_script() => Check("\\overset{a}{x}^2", SSup(LU(R("x"), R("a")), R("2")));

    [Fact] public void Xrightarrow() => Check("\\xrightarrow{a}", LU(R("→"), R("a")));
    [Fact] public void Xrightarrow_below_and_above() =>
        Check("\\xrightarrow[b]{a}", LU(LL(R("→"), R("b")), R("a")));
    [Fact] public void Xleftarrow_empty_above_is_dropped() =>
        Check("\\xleftarrow[b]{}", LL(R("←"), R("b")));
    [Fact] public void Xlongequal() => Check("\\xlongequal{a}", LU(R("="), R("a")), "\\overset{a}{=}");
    [Fact] public void Overset_equals_round_trips() => Check("\\overset{a}{=}", LU(R("="), R("a")));
    [Fact] public void Stackrel_equals_is_overset() => Check("\\stackrel{a}{=}", LU(R("="), R("a")), "\\overset{a}{=}");
    [Fact] public void Overset_def_equals_round_trips() =>
        Check("\\overset{\\text{def}}{=}", LU(R("=") , "<m:r><m:rPr><m:nor/></m:rPr><m:t>def</m:t></m:r>"));
    [Fact] public void XRightarrow() => Check("\\xRightarrow{a}", LU(R("⇒"), R("a")));
    [Fact] public void Xmapsto() => Check("\\xmapsto{a}", LU(R("↦"), R("a")));
    [Fact] public void Xtofrom() => Check("\\xtofrom{a}", LU(R("⇄"), R("a")));
    [Fact] public void Xleftrightarrow() => Check("\\xleftrightarrow{a}", LU(R("↔"), R("a")));
    [Fact] public void Xhookrightarrow() => Check("\\xhookrightarrow{a}", LU(R("↪"), R("a")));
    [Fact] public void Xrightarrow_with_complex_labels() =>
        Check("\\xrightarrow[x+1]{\\alpha}", LU(LL(R("→"), R("x+1")), R("α")));
    [Fact] public void Xrightarrow_missing_argument_throws() =>
        Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\xrightarrow"));
    [Fact] public void Overset_on_non_arrow_base_with_arrow_char_among_text() =>
        Check("\\overset{a}{xy}", LU(R("xy"), R("a")));
    [Fact] public void Overset_on_arrow_with_empty_label_keeps_structure() =>
        Check("\\overset{}{\\rightarrow}", LU(R("→"), ""), "\\overset{}{\\rightarrow}");

    [Fact] public void Limit_function_name_still_writes_as_subscript() =>
        Check("\\lim_{x}f", "<m:func><m:fName>" + LL(R("lim", "<m:sty m:val=\"p\"/>"), R("x"))
            + "</m:fName><m:e>" + R("f") + "</m:e></m:func>", "\\lim_x f");
}
