using System.Text;
using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>Character-producing commands: escapes (<c>\# \$ \% \&amp; \_ \{ \}</c>), <c>\unicode</c>, <c>\char</c>.</summary>
internal static class SymbolCommands
{
    public static void Register(CommandTable t)
    {
        foreach (var c in "#$%&_{}")
            t.Add("\\" + c, (p, name) => LatexParser.CharRun(name[1..]));
        t.Add("\\unicode", Unicode);
        t.Add("\\char", Char);
    }

    /// <summary><c>\unicode{N}</c>: decimal, or hexadecimal when starting with <c>x</c>, <c>X</c> or <c>"</c>.</summary>
    private static FormulaNode Unicode(LatexParser p, string name)
    {
        int pos = p.Position;
        var raw = p.ReadRawGroup().Trim();
        bool hex = raw.Length > 0 && raw[0] is 'x' or 'X' or '"';
        var digits = hex ? raw[1..].Trim() : raw;
        if (!TryParse(digits, hex ? 16 : 10, out var cp))
            throw p.Error($"invalid character code in {name}{{{raw}}}", pos);
        return LatexParser.CharRun(char.ConvertFromUtf32(cp));
    }

    /// <summary><c>\char"HEX</c>, <c>\char'OCTAL</c> or <c>\char DECIMAL</c>; digits must be adjacent.</summary>
    private static FormulaNode Char(LatexParser p, string name)
    {
        int pos = p.Position;
        int radix = 10;
        var first = p.Peek();
        int end = first.Position;
        if (first.Kind == TokenKind.Char && first.Text == "\"") { radix = 16; p.Next(); end = first.End; }
        else if (first.Kind == TokenKind.Prime) { radix = 8; p.Next(); end = first.End; }

        var digits = new StringBuilder();
        while (p.Peek() is { Kind: TokenKind.Char } t && (digits.Length == 0 || t.Position == end) && IsDigit(t.Text, radix))
        {
            digits.Append(t.Text);
            end = t.End;
            p.Next();
        }
        if (!TryParse(digits.ToString(), radix, out var cp))
            throw p.Error($"invalid character code after {name}", pos);
        return LatexParser.CharRun(char.ConvertFromUtf32(cp));
    }

    private static bool IsDigit(string s, int radix) =>
        s.Length == 1 && (radix switch
        {
            16 => char.IsAsciiHexDigit(s[0]),
            8 => s[0] is >= '0' and <= '7',
            _ => char.IsAsciiDigit(s[0]),
        });

    private static bool TryParse(string digits, int radix, out int cp)
    {
        cp = 0;
        if (digits.Length == 0 || digits.Length > 8) return false;
        long value = 0;
        foreach (var c in digits)
        {
            int d = char.IsAsciiDigit(c) ? c - '0' : char.IsAsciiHexDigit(c) ? char.ToLowerInvariant(c) - 'a' + 10 : 99;
            if (d >= radix) return false;
            value = value * radix + d;
        }
        if (value > 0x10FFFF || !Rune.IsValid((int)value)) return false;
        cp = (int)value;
        return true;
    }
}
