namespace FormulaConvert.Tests;

public class PublicSurfaceTests
{
    [Fact]
    public void Core_exports_only_the_converter_api()
    {
        var names = typeof(FormulaConverter).Assembly.GetExportedTypes().Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "ConvertResult", "Diagnostic", "FormulaConverter", "FormulaParseException" }, names);
    }
}
