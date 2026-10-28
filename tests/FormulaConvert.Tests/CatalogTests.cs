using FormulaConvert.Data;
using FormulaConvert.Latex;
using Xunit.Abstractions;

namespace FormulaConvert.Tests;

public class CatalogTests(ITestOutputHelper output)
{
    private static readonly string[] CategoryNames = ["Symbol", "Function", "Nary", "Accent", "Alias", "OutOfScope", "Structural"];

    private static List<string> Documented() =>
        CsvResource.ReadRows("mathlive-commands.txt").Select(r => r[0].Trim()).ToList();

    [Fact]
    public void Every_documented_command_is_classified()
    {
        var documented = Documented();
        var counts = CategoryNames.ToDictionary(c => c, _ => 0);
        int overlap = 0;
        var missing = new List<string>();
        foreach (var c in documented)
        {
            var cats = CommandClassifier.Categories(c);
            if (cats.Count == 0) missing.Add(c);
            if (cats.Count > 1) overlap++;
            foreach (var cat in cats) counts[cat]++;
        }
        output.WriteLine($"Documented commands: {documented.Count}");
        foreach (var (cat, n) in counts) output.WriteLine($"  {cat}: {n}");
        output.WriteLine($"  In more than one category: {overlap}");
        output.WriteLine($"  Unclassified: {missing.Count}");
        Assert.True(missing.Count == 0, $"{missing.Count} unclassified commands: {string.Join(' ', missing)}");
    }

    public static IEnumerable<object[]> Aliases() =>
        CsvResource.ReadRows("aliases.csv", 3).Select(r => new object[] { r[0], int.Parse(r[1]) });

    [Theory]
    [MemberData(nameof(Aliases))]
    public void Every_alias_parses(string command, int arity)
    {
        var latex = command + string.Concat(Enumerable.Repeat("{x}", arity));
        var ex = Record.Exception(() => FormulaConverter.LatexToOmml(latex));
        Assert.True(ex is null, $"{latex}: {ex?.Message}");
    }

    [Fact]
    public void Every_structural_spec_command_is_registered()
    {
        string[] commands = (@"\frac \dfrac \tfrac \cfrac \binom \dbinom \tbinom \over \atop \choose \pdiff \nicefrac \sqrt \left \right \middle \begin \end "
            + @"\hat \vec \overline \underline \overbrace \underbrace \overset \underset \overunderset \stackrel \xrightarrow \boxed \cancel \bcancel \xcancel "
            + @"\enclose \phantom \hphantom \vphantom \smash \mathbf \mathit \mathbfit \mathrm \mathbb \mathcal \mathscr \mathfrak \mathsf \mathtt \text \textrm "
            + @"\operatorname \sum \int \prod \limits \nolimits").Split(' ');
        var missing = commands.Where(c => !CommandTable.Default.Has(c)).ToList();
        Assert.True(missing.Count == 0, "Not registered: " + string.Join(' ', missing));
    }

    [Theory]
    [InlineData("array", "{c}")]
    [InlineData("matrix", "")]
    [InlineData("pmatrix", "")]
    [InlineData("bmatrix", "")]
    [InlineData("Bmatrix", "")]
    [InlineData("vmatrix", "")]
    [InlineData("Vmatrix", "")]
    [InlineData("smallmatrix", "")]
    [InlineData("cases", "")]
    [InlineData("dcases", "")]
    [InlineData("rcases", "")]
    [InlineData("align", "")]
    [InlineData("aligned", "")]
    [InlineData("gather", "")]
    [InlineData("gathered", "")]
    [InlineData("split", "")]
    [InlineData("multline", "")]
    [InlineData("equation", "")]
    [InlineData("math", "")]
    [InlineData("displaymath", "")]
    public void Every_environment_in_spec_is_accepted(string env, string spec)
    {
        var ex = Record.Exception(() => FormulaConverter.LatexToOmml($@"\begin{{{env}}}{spec}a\end{{{env}}}"));
        Assert.True(ex is null, $"{env}: {ex?.Message}");
    }
}
