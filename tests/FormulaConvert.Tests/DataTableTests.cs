using FormulaConvert.Data;
using Xunit;

namespace FormulaConvert.Tests;

public class DataTableTests
{
    [Theory]
    [InlineData("\\alpha", 0x03B1)]
    [InlineData("\\leq", 0x2264)]
    [InlineData("\\rightarrow", 0x2192)]
    [InlineData("\\forall", 0x2200)]
    [InlineData("\\infty", 0x221E)]
    [InlineData("\\partial", 0x2202)]
    [InlineData("\\in", 0x2208)]
    [InlineData("\\cup", 0x222A)]
    [InlineData("\\times", 0x00D7)]
    [InlineData("\\Omega", 0x03A9)]
    [InlineData("\\hbar", 0x210F)]
    [InlineData("\\therefore", 0x2234)]
    [InlineData("\\nabla", 0x2207)]
    [InlineData("\\emptyset", 0x2205)]
    [InlineData("\\Alpha", 0x0391)]
    public void Symbol_pairs_match_unicode(string cmd, int cp)
    {
        Assert.True(SymbolTable.TryGetCodepoint(cmd, out var got));
        Assert.Equal(cp, got);
    }

    [Theory]
    [InlineData(0x2264, "\\leq")]
    [InlineData(0x2192, "\\rightarrow")]
    [InlineData(0x2260, "\\ne")]
    [InlineData(0x2265, "\\geq")]
    public void Reverse_lookup_prefers_canonical(int cp, string cmd)
    {
        Assert.True(SymbolTable.TryGetCommand(cp, out var got));
        Assert.Equal(cmd, got);
    }

    [Theory]
    [InlineData("\\sum", "∑", false)]
    [InlineData("\\int", "∫", true)]
    [InlineData("\\oint", "∮", true)]
    [InlineData("\\prod", "∏", false)]
    [InlineData("\\bigcup", "⋃", false)]
    [InlineData("\\bigoplus", "⨁", false)]
    public void Nary_table(string cmd, string chr, bool subSup)
    {
        Assert.True(NaryTable.TryGetByCommand(cmd, out var info));
        Assert.Equal(chr, info.Chr);
        Assert.Equal(subSup, info.SubSupDefault);
        Assert.True(NaryTable.TryGetByChar(chr, out var back));
        Assert.Equal(cmd, back.Command);
    }

    [Theory]
    [InlineData("\\hat", "̂")]
    [InlineData("\\tilde", "̃")]
    [InlineData("\\vec", "⃗")]
    [InlineData("\\dot", "̇")]
    [InlineData("\\ddot", "̈")]
    [InlineData("\\bar", "̄")]
    [InlineData("\\check", "̌")]
    [InlineData("\\breve", "̆")]
    [InlineData("\\mathring", "̊")]
    public void Accent_table(string cmd, string chr)
    {
        Assert.True(AccentTable.TryGetByCommand(cmd, out var got));
        Assert.Equal(chr, got);
        Assert.True(AccentTable.TryGetByChar(chr, out var back));
        Assert.Equal(cmd, back);
    }

    [Fact]
    public void Functions()
    {
        foreach (var f in new[] { "\\sin", "\\arcsin", "\\lim", "\\gcd" })
            Assert.True(FunctionTable.Contains(f), f);
        Assert.Contains("\\lim", FunctionTable.BoundsFunctions);
        Assert.DoesNotContain("\\sin", FunctionTable.BoundsFunctions);
    }

    [Fact]
    public void Aliases()
    {
        Assert.True(AliasTable.TryGet("\\neq", out var a));
        Assert.Equal(0, a.Arity);
        Assert.Equal("\\ne", a.Replacement);
        Assert.True(AliasTable.TryGet("\\R", out var r));
        Assert.Equal("\\mathbb{R}", r.Replacement);
    }

    [Fact]
    public void OutOfScope_table_loads()
    {
        Assert.False(OutOfScopeTable.TryGet("\\nonexistent", out _));
    }

    [Fact]
    public void Symbols_are_at_least_480()
    {
        var n = CsvResource.ReadRows("symbols.csv").Count();
        Assert.True(n >= 480, n.ToString());
    }

    [Fact]
    public void No_symbol_uses_private_use_area()
    {
        foreach (var r in CsvResource.ReadRows("symbols.csv"))
        {
            int cp = CsvResource.ParseHex(r[1]);
            Assert.False(cp is >= 0xE000 and <= 0xF8FF, $"{r[0]} uses PUA codepoint U+{cp:X4}");
        }
    }

    [Theory]
    [InlineData("\\iddots", 0x22F0)]
    [InlineData("\\nequiv", 0x2262)]
    [InlineData("\\rightangle", 0x221F)]
    [InlineData("\\ratio", 0x2236)]
    [InlineData("\\qed", 0x220E)]
    [InlineData("\\AA", 0x212B)]
    [InlineData("\\dotminus", 0x2238)]
    [InlineData("\\dlsh", 0x21B2)]
    [InlineData("\\drsh", 0x21B3)]
    public void Word_gallery_symbols_exist(string cmd, int cp)
    {
        Assert.True(SymbolTable.TryGetCodepoint(cmd, out var got));
        Assert.Equal(cp, got);
    }
}
