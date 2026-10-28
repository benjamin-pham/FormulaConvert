namespace FormulaConvert.Tests.Harness;

public static class CaseRunner
{
    /// <summary>
    /// (a) LatexToOmml(Latex) equals the expected OMML; (b) OmmlToLatex(expected OMML) equals the canonical LaTeX;
    /// (c) LatexToOmml of that canonical output gives the original OMML back.
    /// </summary>
    public static void Check(ConversionCase c)
    {
        var expectedOmml = Xml.OMath(c.OmmlInner);
        var expectedLatex = c.CanonicalLatex ?? c.Latex;

        var omml = Convert(() => FormulaConverter.LatexToOmml(c.Latex).Output, c, "LatexToOmml(Latex)");
        Assert.True(Xml.SameXml(expectedOmml, omml),
            $"(a) LatexToOmml mismatch for Latex `{c.Latex}`\n  expected: {expectedOmml}\n  actual:   {omml}");

        var latex = Convert(() => FormulaConverter.OmmlToLatex(expectedOmml).Output, c, "OmmlToLatex(OmmlInner)");
        Assert.True(expectedLatex == latex,
            $"(b) OmmlToLatex mismatch for Latex `{c.Latex}`\n  expected: `{expectedLatex}`\n  actual:   `{latex}`");

        var again = Convert(() => FormulaConverter.LatexToOmml(latex).Output, c, "LatexToOmml(OmmlToLatex(...))");
        Assert.True(Xml.SameXml(expectedOmml, again),
            $"(c) round trip mismatch for Latex `{c.Latex}` (via `{latex}`)\n  expected: {expectedOmml}\n  actual:   {again}");
    }

    private static string Convert(Func<string> f, ConversionCase c, string step)
    {
        try
        {
            return f();
        }
        catch (FormulaParseException ex)
        {
            Assert.Fail($"{step} threw for Latex `{c.Latex}`: {ex.Message} (position {ex.Position})");
            throw;
        }
    }
}
