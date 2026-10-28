using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class BoxTests
{
    private static void Check(string latex, string ommlInner, string? canonical = null) =>
        CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical));

    private static readonly string[] FlagOrder = ["hideTop", "hideBot", "hideLeft", "hideRight", "strikeH", "strikeV", "strikeBLTR", "strikeTLBR"];

    /// <summary>m:borderBox with the given flags (written in schema order); no m:borderBoxPr when there are none.</summary>
    private static string Bb(string[] flags, string e)
    {
        var pr = string.Concat(FlagOrder.Where(flags.Contains).Select(f => $"<m:{f} m:val=\"1\"/>"));
        return $"<m:borderBox>{(pr.Length == 0 ? "" : $"<m:borderBoxPr>{pr}</m:borderBoxPr>")}<m:e>{e}</m:e></m:borderBox>";
    }

    private static readonly string[] Hide4 = ["hideTop", "hideBot", "hideLeft", "hideRight"];

    private static string Ph(string[] flags, string e)
    {
        var order = new[] { "show", "zeroWid", "zeroAsc", "zeroDesc" };
        var pr = string.Concat(order.Where(flags.Contains).Select(f => $"<m:{f} m:val=\"{(f == "show" ? 0 : 1)}\"/>"));
        return $"<m:phant>{(pr.Length == 0 ? "" : $"<m:phantPr>{pr}</m:phantPr>")}<m:e>{e}</m:e></m:phant>";
    }

    private static readonly string X = R("x");

    [Fact] public void Boxed() => Check("\\boxed{x}", Bb([], X));
    [Fact] public void Cancel() => Check("\\cancel{x}", Bb([.. Hide4, "strikeBLTR"], X));
    [Fact] public void Bcancel() => Check("\\bcancel{x}", Bb([.. Hide4, "strikeTLBR"], X));
    [Fact] public void Xcancel() => Check("\\xcancel{x}", Bb([.. Hide4, "strikeBLTR", "strikeTLBR"], X));
    [Fact] public void Enclose_box() => Check("\\enclose{box}{x}", Bb([], X), "\\boxed{x}");
    [Fact] public void Enclose_top_bottom() => Check("\\enclose{top bottom}{x}", Bb(["hideLeft", "hideRight"], X));
    [Fact] public void Enclose_left() => Check("\\enclose{left}{x}", Bb(["hideTop", "hideBot", "hideRight"], X));
    [Fact] public void Enclose_box_strike() => Check("\\enclose{box horizontalstrike}{x}", Bb(["strikeH"], X));
    [Fact] public void Enclose_horizontalstrike() => Check("\\enclose{horizontalstrike}{x}", Bb([.. Hide4, "strikeH"], X));
    [Fact] public void Enclose_verticalstrike() => Check("\\enclose{verticalstrike}{x}", Bb([.. Hide4, "strikeV"], X));
    [Fact] public void Enclose_updiagonalstrike() => Check("\\enclose{updiagonalstrike}{x}", Bb([.. Hide4, "strikeBLTR"], X), "\\cancel{x}");
    [Fact] public void Enclose_downdiagonalstrike() => Check("\\enclose{downdiagonalstrike}{x}", Bb([.. Hide4, "strikeTLBR"], X), "\\bcancel{x}");
    [Fact] public void Enclose_both_diagonals() => Check("\\enclose{updiagonalstrike downdiagonalstrike}{x}", Bb([.. Hide4, "strikeBLTR", "strikeTLBR"], X), "\\xcancel{x}");
    [Fact] public void Enclose_notation_order_is_canonical() => Check("\\enclose{bottom top}{x}", Bb(["hideLeft", "hideRight"], X), "\\enclose{top bottom}{x}");

    [Fact]
    public void Enclose_roundedbox_is_lossy()
    {
        var r = FormulaConverter.LatexToOmml("\\enclose{roundedbox}{x}");
        Assert.Equal("lossy", Assert.Single(r.Diagnostics).Code);
        Check("\\enclose{roundedbox}{x}", Bb([], X), "\\boxed{x}");
    }

    [Fact]
    public void Enclose_circle_with_style_is_dropped()
    {
        var r = FormulaConverter.LatexToOmml("\\enclose{circle}[mathcolor=\"red\"]{x}");
        Assert.True(SameXml(OMath(X), r.Output));
        Assert.Equal(["out-of-scope", "out-of-scope"], r.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public void Enclose_unknown_notation_keeps_the_rest()
    {
        var r = FormulaConverter.LatexToOmml("\\enclose{top radical}{x}");
        Assert.Equal("out-of-scope", Assert.Single(r.Diagnostics).Code);
        Assert.True(SameXml(OMath(Bb(["hideBot", "hideLeft", "hideRight"], X)), r.Output));
    }

    [Fact] public void Phantom() => Check("\\phantom{x}", Ph(["show"], X));
    [Fact] public void Hphantom() => Check("\\hphantom{x}", Ph(["show", "zeroAsc", "zeroDesc"], X));
    [Fact] public void Vphantom() => Check("\\vphantom{x}", Ph(["show", "zeroWid"], X));
    [Fact] public void Smash() => Check("\\smash{x}", Ph(["zeroAsc", "zeroDesc"], X));
    [Fact] public void Smash_optional_argument_is_dropped() => Check("\\smash[b]{x}", Ph(["zeroAsc", "zeroDesc"], X), "\\smash{x}");
    [Fact] public void Phantom_all_zero() => Check("\\hphantom{\\vphantom{x}}", Ph(["show", "zeroAsc", "zeroDesc"], Ph(["show", "zeroWid"], X)));
    [Fact] public void Box_in_row() => Check("a\\boxed{x}b", R("a") + Bb([], X) + R("b"));

    [Fact]
    public void Missing_argument_throws()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.LatexToOmml("\\boxed"));
        Assert.True(ex.Position >= 0);
    }

    private static string Wrap(string inner) => OMath(inner);

    [Theory]
    [InlineData("on")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("")]
    public void Reader_accepts_on_values(string val)
    {
        var attr = val.Length == 0 ? "" : $" m:val=\"{val}\"";
        var omml = Wrap($"<m:borderBox><m:borderBoxPr><m:ctrlPr/><m:hideTop{attr}/><m:hideBot{attr}/><m:hideLeft{attr}/><m:hideRight{attr}/><m:strikeH{attr}/></m:borderBoxPr><m:e>{X}</m:e></m:borderBox>");
        Assert.Equal("\\enclose{horizontalstrike}{x}", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("false")]
    public void Reader_accepts_off_values(string val)
    {
        var omml = Wrap($"<m:borderBox><m:borderBoxPr><m:hideTop m:val=\"{val}\"/><m:hideBot m:val=\"{val}\"/><m:hideLeft m:val=\"{val}\"/><m:hideRight m:val=\"{val}\"/><m:strikeH m:val=\"{val}\"/></m:borderBoxPr><m:e>{X}</m:e></m:borderBox>");
        Assert.Equal("\\boxed{x}", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Reader_phantom_full_form()
    {
        var omml = Wrap($"<m:phant><m:phantPr><m:ctrlPr/><m:show m:val=\"on\"/><m:zeroWid m:val=\"0\"/><m:zeroAsc m:val=\"1\"/><m:zeroDesc m:val=\"true\"/></m:phantPr><m:e>{X}</m:e></m:phant>");
        Assert.Equal("\\smash{x}", FormulaConverter.OmmlToLatex(omml).Output);
    }

    [Fact]
    public void Writer_shown_phantom_without_a_command_is_the_body()
    {
        var omml = Wrap($"<m:phant><m:phantPr><m:zeroWid m:val=\"1\"/></m:phantPr><m:e>{X}</m:e></m:phant>");
        Assert.Equal("x", FormulaConverter.OmmlToLatex(omml).Output);
    }
}
