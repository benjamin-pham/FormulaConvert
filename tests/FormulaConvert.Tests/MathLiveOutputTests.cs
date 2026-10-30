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

    /// <summary>TeX control symbols the writer uses that MathLive reads as ordinary escapes.</summary>
    private static readonly HashSet<string> ControlSymbols = [@"\\", @"\&", @"\%", @"\#", @"\$", @"\_", @"\{", @"\}"];

    private static void CollectNonMathLiveCommands(string latex, string source, List<string> violations)
    {
        foreach (Match m in Regex.Matches(latex, @"\\(?:begin|end)\{([^}]+)\}"))
            if (!MathLiveCatalog.Environments.Contains(m.Groups[1].Value))
                violations.Add($"environment {m.Groups[1].Value} in `{latex}` from {source}");
        foreach (Match m in Regex.Matches(latex, @"\\(?:[A-Za-z]+|[^A-Za-z\s]| )"))
        {
            var cmd = m.Value;
            if (cmd is @"\begin" or @"\end" || ControlSymbols.Contains(cmd)) continue;
            if (!MathLiveCatalog.Defined.Contains(cmd))
                violations.Add($"{cmd} in `{latex}` from {source}");
        }
    }

    [Fact]
    public void Round_tripping_any_mathlive_command_only_writes_mathlive_commands()
    {
        // § stands for the command under test; every template that parses is converted to OMML and back.
        string[] templates =
        [
            "§", "§{x}", "§{x}{y}", "§{x}{y}{z}", "§[x]{y}", "§{(}{)}{0pt}{}{x}{y}",
            "a § b", "§_a^b z", "\\left(§\\right)", "\\sqrt[§]{x}", "{§}^2", "§{x}^2_i",
        ];
        var violations = new List<string>();
        var crashes = new List<string>();
        int converted = 0;
        foreach (var command in MathLiveCatalog.Defined)
        {
            foreach (var template in templates)
            {
                var latex = template.Replace("§", command);
                try
                {
                    var omml = FormulaConverter.LatexToOmml(latex).Output;
                    CollectNonMathLiveCommands(FormulaConverter.OmmlToLatex(omml).Output, latex, violations);
                    converted++;
                }
                catch (FormulaParseException)
                {
                }
                catch (Exception ex)
                {
                    crashes.Add($"`{latex}`: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        foreach (var env in MathLiveCatalog.Environments.Select(e => e["env:".Length..]))
        {
            foreach (var latex in new[] { $"\\begin{{{env}}}a&b\\\\c&d\\end{{{env}}}", $"\\begin{{{env}}}{{cc}}a&b\\end{{{env}}}" })
            {
                try
                {
                    var omml = FormulaConverter.LatexToOmml(latex).Output;
                    CollectNonMathLiveCommands(FormulaConverter.OmmlToLatex(omml).Output, latex, violations);
                }
                catch (FormulaParseException)
                {
                }
                catch (Exception ex)
                {
                    crashes.Add($"`{latex}`: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        Assert.True(converted > 5000, $"only {converted} templates converted; the test is no longer exercising the writers");
        Assert.True(crashes.Count == 0, $"{crashes.Count} crashes: {string.Join("; ", crashes.Take(10))}");
        var distinct = violations.Distinct().ToList();
        Assert.True(distinct.Count == 0, $"{distinct.Count} non-MathLive commands written: {string.Join("; ", distinct.Take(25))}");
    }
}
