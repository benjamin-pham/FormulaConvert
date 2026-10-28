using FormulaConvert.Cli;

namespace FormulaConvert.Tests;

public class CliTests
{
    private static (int Code, string Out, string Err) Run(string stdin, params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        int code = CliApp.Run(args, new StringReader(stdin), o, e);
        return (code, o.ToString(), e.ToString());
    }

    [Fact]
    public void Latex2omml_reads_stdin()
    {
        var (code, o, e) = Run("\\frac{a}{b}\n", "latex2omml");
        Assert.Equal(0, code);
        Assert.Contains("<m:f>", o);
        Assert.EndsWith("\n", o);
        Assert.False(o.EndsWith("\n\n"));
        Assert.Equal("", e);
    }

    [Fact]
    public void Omml2latex_reads_file()
    {
        var omml = FormulaConverter.LatexToOmml("\\frac{a}{b}").Output;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, omml);
            var (code, o, _) = Run("", "omml2latex", path);
            Assert.Equal(0, code);
            Assert.Equal("\\frac{a}{b}\n", o);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Warnings_go_to_stderr()
    {
        var (code, o, e) = Run("\\textcolor{red}{x}", "latex2omml");
        Assert.Equal(0, code);
        Assert.Contains("warning [out-of-scope] position ", e);
        Assert.Contains("<m:oMath", o);
    }

    [Fact]
    public void Parse_error_exits_1()
    {
        var (code, o, e) = Run("\\frac{a", "latex2omml");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.StartsWith("error:", e);
        Assert.Contains("position", e);
    }

    [Fact]
    public void Missing_file_exits_1()
    {
        var (code, o, e) = Run("", "omml2latex", "/no/such/file.xml");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.StartsWith("error:", e);
    }

    [Fact]
    public void Unknown_subcommand_exits_1()
    {
        var (code, o, e) = Run("", "bogus");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.Contains("Usage", e);
    }

    [Fact]
    public void No_args_exits_1()
    {
        var (code, o, e) = Run("");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.Contains("Usage", e);
    }

    [Fact]
    public void Malformed_xml_exits_1()
    {
        var (code, o, e) = Run("<m:oMath", "omml2latex");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.StartsWith("error:", e);
    }

    [Fact]
    public void Too_many_args_exits_1()
    {
        var (code, o, e) = Run("", "omml2latex", "a", "b");
        Assert.Equal(1, code);
        Assert.Equal("", o);
        Assert.Contains("Usage", e);
    }
}
