using System.Diagnostics;
using System.Text;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class DepthTests
{
    private static string Nested(int levels)
    {
        // oMath is level 0; each m:f/m:num pair adds two levels, an extra m:e adds one.
        var sb = new StringBuilder($"<m:oMath xmlns:m=\"{MathNamespace}\">");
        for (var i = 0; i < levels / 2; i++) sb.Append("<m:f><m:num>");
        var odd = levels % 2 == 1;
        if (odd) sb.Append("<m:e>");
        if (odd) sb.Append("</m:e>");
        for (var i = 0; i < levels / 2; i++) sb.Append("</m:num></m:f>");
        return sb.Append("</m:oMath>").ToString();
    }

    [Fact]
    public void Very_deep_document_is_rejected_quickly()
    {
        var xml = Nested(100_000);
        var sw = Stopwatch.StartNew();
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.OmmlToLatex(xml));
        sw.Stop();
        Assert.Equal(-1, ex.Position);
        Assert.Contains("nesting too deep", ex.Message);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"took {sw.Elapsed}");
    }

    [Fact]
    public void Depth_at_limit_is_accepted() => FormulaConverter.OmmlToLatex(Nested(NestingGuard.XmlLimit));

    [Fact]
    public void Depth_above_limit_is_rejected()
    {
        var ex = Assert.Throws<FormulaParseException>(() => FormulaConverter.OmmlToLatex(Nested(NestingGuard.XmlLimit + 1)));
        Assert.Equal(-1, ex.Position);
    }
}
