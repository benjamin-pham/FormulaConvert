using System.Text.RegularExpressions;
using FormulaConvert.Ast;
using FormulaConvert.Data;
using FormulaConvert.Latex;
using FormulaConvert.Tests.Harness;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class MathLiveOutputTests
{
    [Fact]
    public void Catalog_has_canaries()
    {
        string[] commandCanaries =
        [
            @"\frac", @"\nicefrac", @"\left", @"\right", @"\middle",
            @"\hat", @"\vec", @"\tilde", @"\check", @"\deg",
            @"\bmod", @"\pmod", @"\mathscr", @"\overrightarrow", @"\widehat",
            @"\mathrlap", @"\mathrel", @"\quad", @"\>", @"\enskip"
        ];
        string[] envCanaries = ["env:aligned", "env:gathered", "env:Vmatrix", "env:rcases"];

        foreach (var c in commandCanaries)
        {
            Assert.True(MathLiveCatalog.Defined.Contains(c), $"Expected command canary {c} to be in MathLiveCatalog.Defined");
        }
        foreach (var e in envCanaries)
        {
            Assert.True(MathLiveCatalog.Environments.Contains(e), $"Expected environment canary {e} to be in MathLiveCatalog.Environments");
        }

        Assert.DoesNotContain(@"\sech", MathLiveCatalog.Defined);
        Assert.DoesNotContain(@"\csch", MathLiveCatalog.Defined);
    }

    [Fact]
    public void Every_emitted_command_is_defined_in_mathlive()
    {
        var violations = new List<string>();

        void CheckNode(FormulaNode node, string source)
        {
            var latex = LatexWriter.Write(node);
            foreach (Match m in Regex.Matches(latex, @"\\(?:begin|end)\{([^}]+)\}"))
            {
                var env = m.Groups[1].Value;
                if (!MathLiveCatalog.Environments.Contains(env))
                    violations.Add($"Environment '{env}' emitted by {source} (`{latex}`) is not defined in MathLive");
            }
            foreach (Match m in Regex.Matches(latex, @"\\(?:[A-Za-z]+|[^A-Za-z\s]| )"))
            {
                var cmd = m.Value;
                if (cmd is @"\begin" or @"\end") continue;
                if (!MathLiveCatalog.Defined.Contains(cmd))
                    violations.Add($"Command '{cmd}' emitted by {source} (`{latex}`) is not defined in MathLive");
            }
        }

        var seenCodepoints = new HashSet<int>();

        foreach (var r in CsvResource.ReadRows("symbols.csv"))
        {
            var cp = CsvResource.ParseHex(r[1]);
            if (!seenCodepoints.Add(cp)) continue;
            var node = new Run(char.ConvertFromUtf32(cp), new RunStyle());
            CheckNode(node, $"symbols.csv U+{cp:X4} ({r[0]})");
        }

        foreach (var r in CsvResource.ReadRows("nary.csv", 3))
        {
            var cp = CsvResource.ParseHex(r[1]);
            if (!seenCodepoints.Add(cp)) continue;
            var node = new Nary(char.ConvertFromUtf32(cp), null, null, new Run("x", new RunStyle()), false);
            CheckNode(node, $"nary.csv U+{cp:X4} ({r[0]})");
        }

        foreach (var r in CsvResource.ReadRows("accents.csv"))
        {
            var cp = CsvResource.ParseHex(r[1]);
            var node = new Accent(char.ConvertFromUtf32(cp), new Run("x", new RunStyle()));
            CheckNode(node, $"accents.csv U+{cp:X4} ({r[0]})");
        }

        foreach (var r in CsvResource.ReadRows("functions.txt"))
        {
            var name = r[0].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var fnName = name.StartsWith('\\') ? name[1..] : name;
            var node = new FunctionApply(new Run(fnName, new RunStyle(Sty.Plain)), new Run("x", new RunStyle()));
            CheckNode(node, $"functions.txt {name}");
        }

        Assert.True(violations.Count == 0, $"{violations.Count} commands not defined in MathLive:\n" + string.Join("\n", violations.Distinct()));
    }

    [Fact]
    public void Parse_only_symbols_still_parse()
    {
        var r = FormulaConverter.LatexToOmml(@"\nequiv");
        Assert.True(SameXml(OMath(R("≢")), r.Output));
    }

    [Fact]
    public void Parse_only_symbols_are_written_by_kind()
    {
        Assert.Equal(@"a\mathrel{≢}b", FormulaConverter.OmmlToLatex(OMath(R("a≢b"))).Output);
        Assert.Equal("∟", FormulaConverter.OmmlToLatex(OMath(R("∟"))).Output);
        Assert.Equal("¶", FormulaConverter.OmmlToLatex(OMath(R("¶"))).Output);

        var funcOmml = "<m:func><m:fName><m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>sech</m:t></m:r></m:fName><m:e><m:r><m:t>x</m:t></m:r></m:e></m:func>";
        Assert.Equal(@"\operatorname{sech}x", FormulaConverter.OmmlToLatex(OMath(funcOmml)).Output);

        var roundTrip = FormulaConverter.LatexToOmml(@"a\mathrel{≢}b");
        Assert.True(SameXml(OMath(R("a≢b")), roundTrip.Output));
        Assert.Empty(roundTrip.Diagnostics);
    }

    [Fact]
    public void Mathrel_with_several_characters_still_warns()
    {
        var r = FormulaConverter.LatexToOmml(@"\mathrel{ab}");
        Assert.Single(r.Diagnostics);
        Assert.Equal("out-of-scope", r.Diagnostics[0].Code);
    }

    [Fact]
    public void Parse_only_symbol_in_text_is_literal()
    {
        CaseRunner.Check(new ConversionCase(@"\text{≢}", R("≢", "<m:nor/>")));
    }
}
