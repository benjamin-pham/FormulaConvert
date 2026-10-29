using System.Text;
using System.Xml.Linq;
using FormulaConvert.Data;
using static FormulaConvert.Tests.Harness.Xml;

namespace FormulaConvert.Tests;

public class SymbolSweepTests
{
    public static IEnumerable<object[]> Rows() =>
        CsvResource.ReadRows("symbols.csv").Select(r => new object[] { r[0], r[1] });

    // N-ary commands (\coprod, \oint) parse to Nary nodes, not m:t text; covered by NaryFunctionTests.
    public static IEnumerable<object[]> ParseRows() =>
        Rows().Where(r => !NaryTable.TryGetByCommand((string)r[0], out _));

    [Theory]
    [MemberData(nameof(ParseRows))]
    public void Command_parses_to_its_codepoint(string command, string hex)
    {
        _ = hex;
        Assert.True(SymbolTable.TryGetCodepoint(command, out var cp));
        var omml = XElement.Parse(FormulaConverter.LatexToOmml(command).Output);
        XNamespace m = MathNamespace;
        var text = string.Concat(omml.Descendants(m + "t").Select(t => t.Value));
        Assert.Equal(char.ConvertFromUtf32(cp), text);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Codepoint_writes_canonical_command(string command, string hex)
    {
        _ = command;
        var cp = CsvResource.ParseHex(hex);
        var ch = char.ConvertFromUtf32(cp);
        Assert.Equal(Expected(cp, ch), FormulaConverter.OmmlToLatex(OMath(R(ch))).Output);
    }

    public static IEnumerable<object[]> NonAsciiCodepoints() =>
        CsvResource.ReadRows("symbols.csv").Select(r => CsvResource.ParseHex(r[1])).Where(cp => cp >= 0x80)
            .Distinct().Select(cp => new object[] { cp });

    [Theory]
    [MemberData(nameof(NonAsciiCodepoints))]
    public void Codepoint_round_trips(int cp)
    {
        var ch = char.ConvertFromUtf32(cp);
        var latex = FormulaConverter.OmmlToLatex(OMath(R(ch))).Output;
        var omml = XElement.Parse(FormulaConverter.LatexToOmml(latex).Output);
        Assert.Equal(ch, string.Concat(omml.Descendants(XName.Get("t", MathNamespace)).Select(t => t.Value)));
    }

    [Theory]
    [InlineData("é")]
    [InlineData("中")]
    [InlineData("𝄞")]
    [InlineData("ж")]
    public void Unlisted_character_passes_through(string ch)
    {
        Assert.False(SymbolTable.TryGetCommand(Rune.GetRuneAt(ch, 0).Value, out _));
        Assert.Equal(ch, FormulaConverter.OmmlToLatex(OMath(R(ch))).Output);
    }

    private static string Expected(int cp, string ch)
    {
        if (cp is '^' or '~' or '\'') return $"\\char\"{cp:X2}"; // literal script marks round-trip via \char
        if (cp < 0x80)
            return ch == "\\" ? "\\backslash" : "#$%&_{}".Contains(ch) ? "\\" + ch : ch;
        if (NaryTable.TryGetByChar(ch, out _)) return ch; // plain runs keep n-ary characters literal
        if (SymbolTable.TryGetCommand(cp, out var cmd)) return cmd;
        if (SymbolTable.TryGetParseOnlyKind(cp, out var kind))
            return kind switch
            {
                "rel" => $"\\mathrel{{{ch}}}",
                "bin" => $"\\mathbin{{{ch}}}",
                _ => ch,
            };
        return ch;
    }
}
