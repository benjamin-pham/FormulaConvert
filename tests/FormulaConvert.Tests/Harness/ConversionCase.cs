namespace FormulaConvert.Tests.Harness;

/// <summary>
/// One two-way conversion case: <paramref name="Latex"/> parses to <c>&lt;m:oMath&gt;</c> + <paramref name="OmmlInner"/>,
/// and that OMML writes back as <paramref name="CanonicalLatex"/> (or <paramref name="Latex"/> when null).
/// </summary>
public sealed record ConversionCase(string Latex, string OmmlInner, string? CanonicalLatex = null);
