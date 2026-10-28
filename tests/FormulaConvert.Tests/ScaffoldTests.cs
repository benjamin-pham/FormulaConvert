using FormulaConvert.Ast;

namespace FormulaConvert.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Frac_records_expose_parts()
    {
        var f = new Frac(new Run("a", new RunStyle()), new Run("b", new RunStyle()), FracKind.Bar);
        Assert.Equal(FracKind.Bar, f.Kind);
        Assert.Equal("a", ((Run)f.Num).Text);
        Assert.Empty(FormulaRow.Empty.Items);
        Assert.Equal(3, new FormulaParseException("x", 3).Position);
    }
}
