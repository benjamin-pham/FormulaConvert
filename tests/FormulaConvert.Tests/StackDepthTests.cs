using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

/// <summary>Deep input must fail with <see cref="FormulaParseException"/> (never a stack overflow) on a 1 MB stack, the Windows default.</summary>
public class StackDepthTests
{
    private const int SmallStack = 1024 * 1024;

    /// <summary>Runs <paramref name="action"/> on a thread with a 1 MB stack and returns what it threw (null on success).</summary>
    private static Exception? OnSmallStack(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { caught = e; }
        }, SmallStack);
        thread.Start();
        thread.Join();
        return caught;
    }

    private static void AssertSucceeds(Action action)
    {
        if (OnSmallStack(action) is { } ex) Assert.Fail(ex.ToString());
    }

    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    public static TheoryData<string> DeepLatex() => new()
    {
        Repeat("\\left(", 700) + "x" + Repeat("\\right)", 700),
        Repeat("\\begin{matrix}", 500) + "x" + Repeat("\\end{matrix}", 500),
        Repeat("\\begin{pmatrix}", 500) + "x" + Repeat("\\end{pmatrix}", 500),
        Repeat("\\sum", 999) + "x",
        Repeat("\\sin", 999) + "x",
        Repeat("{}^a", 999) + "x",
        Repeat("\\frac{", 999) + "x" + Repeat("}{y}", 999),
        Repeat("{", 999) + "x" + Repeat("}", 999),
        Repeat("\\sqrt{", 999) + "x" + Repeat("}", 999),
    };

    [Theory]
    [MemberData(nameof(DeepLatex))]
    public void Deep_latex_throws_parse_exception_on_small_stack(string latex)
    {
        var ex = OnSmallStack(() => FormulaConverter.LatexToOmml(latex));
        var parse = Assert.IsType<FormulaParseException>(ex);
        Assert.Contains("nesting too deep", parse.Message);
    }

    private static string Wrap(string open, string close, int n) =>
        OMath(Repeat(open, n) + R("x") + Repeat(close, n));

    public static TheoryData<string> DeepOmml() => new()
    {
        Wrap("<m:d><m:e>", "</m:e></m:d>", 600),
        Wrap("<m:f><m:num>", "</m:num><m:den></m:den></m:f>", 600),
        Wrap("<m:m><m:mr><m:e>", "</m:e></m:mr></m:m>", 400),
        Wrap("<m:nary><m:e>", "</m:e></m:nary>", 600),
        Wrap("<m:foo>", "</m:foo>", 1200),
    };

    [Theory]
    [MemberData(nameof(DeepOmml))]
    public void Deep_omml_throws_parse_exception_on_small_stack(string omml)
    {
        var ex = OnSmallStack(() => FormulaConverter.OmmlToLatex(omml));
        var parse = Assert.IsType<FormulaParseException>(ex);
        Assert.Contains("nesting too deep", parse.Message);
    }

    /// <summary>LaTeX nested as deeply as the parser allows converts to OMML that the reader accepts, on a small stack.</summary>
    public static TheoryData<string, string> AtLimit() => new()
    {
        { "\\left(", "\\right)" },
        { "\\begin{pmatrix}", "\\end{pmatrix}" },
        { "\\begin{matrix}", "\\end{matrix}" },
        { "\\frac{", "}{y}" },
        { "\\sqrt{", "}" },
        { "x^{", "}" },
        { "\\overbrace{", "}^{a}" },
        { "\\underset{a}{", "}" },
        { "\\sum_{", "}x" },
        { "\\sin{", "}" },
        { "\\begin{cases}", "\\end{cases}" },
        { "\\boxed{", "}" },
    };

    [Theory]
    [MemberData(nameof(AtLimit))]
    public void Deepest_accepted_latex_round_trips_on_small_stack(string open, string close)
    {
        // Find the deepest nesting the parser accepts, then check its OMML converts back.
        int n = NestingGuard.LatexLimit;
        string? omml = null;
        for (; n > 0 && omml == null; n--)
        {
            var latex = Repeat(open, n) + "x" + Repeat(close, n);
            Exception? ex = OnSmallStack(() => omml = FormulaConverter.LatexToOmml(latex).Output);
            if (ex != null) Assert.IsType<FormulaParseException>(ex);
        }
        Assert.NotNull(omml);
        Assert.True(n > NestingGuard.LatexLimit / 3, $"only {n} levels accepted");
        AssertSucceeds(() => FormulaConverter.OmmlToLatex(omml!));
    }

    [Fact]
    public void Omml_at_xml_limit_is_accepted_on_small_stack()
    {
        // m:f/m:num pairs: two XML levels each; the innermost m:r/m:t adds two more.
        var omml = Wrap("<m:f><m:num>", "</m:num><m:den></m:den></m:f>", NestingGuard.XmlLimit / 2 - 1);
        AssertSucceeds(() => FormulaConverter.OmmlToLatex(omml));
        // Rows at every level (an extra run beside each nested delimiter).
        var rows = Wrap("<m:d><m:e>" + R("a"), "</m:e></m:d>", NestingGuard.XmlLimit / 2 - 1);
        AssertSucceeds(() => FormulaConverter.OmmlToLatex(rows));
    }
}
