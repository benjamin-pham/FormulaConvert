using System.Text;
using FormulaConvert.Ast;

namespace FormulaConvert.Omml;

internal static class MathAlphaDecoder
{
    private static readonly RunStyle BoldRoman = new(Sty.Bold, Scr.Roman);
    private static readonly RunStyle ItalicRoman = new(Sty.Italic, Scr.Roman);
    private static readonly RunStyle BoldItalicRoman = new(Sty.BoldItalic, Scr.Roman);
    private static readonly RunStyle ScriptItalic = new(Sty.Italic, Scr.Script);
    private static readonly RunStyle BoldScript = new(Sty.Bold, Scr.Script);
    private static readonly RunStyle FrakturItalic = new(Sty.Italic, Scr.Fraktur);
    private static readonly RunStyle BoldFraktur = new(Sty.Bold, Scr.Fraktur);
    private static readonly RunStyle DoubleStruckItalic = new(Sty.Italic, Scr.DoubleStruck);
    private static readonly RunStyle SansSerifItalic = new(Sty.Italic, Scr.SansSerif);
    private static readonly RunStyle SansSerifBold = new(Sty.Bold, Scr.SansSerif);
    private static readonly RunStyle SansSerifBoldItalic = new(Sty.BoldItalic, Scr.SansSerif);
    private static readonly RunStyle MonospaceItalic = new(Sty.Italic, Scr.Monospace);

    // Holes in 0x1D400..0x1D6A3 that are reserved/unassigned in Unicode
    // (their characters were defined in BMP Letterlike block or omitted).
    private static bool IsHole(int cp) => cp switch
    {
        0x1D455 => true,
        0x1D49D or 0x1D4A0 or 0x1D4A1 or 0x1D4A3 or 0x1D4A4 or 0x1D4A7 or 0x1D4A8 or 0x1D4AD => true,
        0x1D4BA or 0x1D4BC or 0x1D4C4 => true,
        0x1D506 or 0x1D50B or 0x1D50C or 0x1D515 or 0x1D51D => true,
        0x1D53A or 0x1D53F or 0x1D545 or 0x1D547 or 0x1D548 or 0x1D549 or 0x1D551 => true,
        _ => false,
    };

    public static bool TryDecode(Rune rune, out char ascii, out RunStyle style)
    {
        int cp = rune.Value;

        // 1. BMP Letterlike styled letters
        switch (cp)
        {
            // Double-Struck
            case 0x2102: ascii = 'C'; style = DoubleStruckItalic; return true;
            case 0x2115: ascii = 'N'; style = DoubleStruckItalic; return true;
            case 0x2119: ascii = 'P'; style = DoubleStruckItalic; return true;
            case 0x211A: ascii = 'Q'; style = DoubleStruckItalic; return true;
            case 0x211D: ascii = 'R'; style = DoubleStruckItalic; return true;
            case 0x2124: ascii = 'Z'; style = DoubleStruckItalic; return true;

            // Script
            case 0x212C: ascii = 'B'; style = ScriptItalic; return true;
            case 0x2130: ascii = 'E'; style = ScriptItalic; return true;
            case 0x2131: ascii = 'F'; style = ScriptItalic; return true;
            case 0x210B: ascii = 'H'; style = ScriptItalic; return true;
            case 0x2110: ascii = 'I'; style = ScriptItalic; return true;
            case 0x2112: ascii = 'L'; style = ScriptItalic; return true;
            case 0x2133: ascii = 'M'; style = ScriptItalic; return true;
            case 0x211B: ascii = 'R'; style = ScriptItalic; return true;
            case 0x212F: ascii = 'e'; style = ScriptItalic; return true;
            case 0x210A: ascii = 'g'; style = ScriptItalic; return true;
            case 0x2134: ascii = 'o'; style = ScriptItalic; return true;

            // Fraktur
            case 0x212D: ascii = 'C'; style = FrakturItalic; return true;
            case 0x210C: ascii = 'H'; style = FrakturItalic; return true;
            case 0x2128: ascii = 'Z'; style = FrakturItalic; return true;

            // Italic h
            case 0x210E: ascii = 'h'; style = ItalicRoman; return true;
        }

        // 2. Mathematical Alphanumeric Symbols: Latin (U+1D400..U+1D6A3)
        if (cp is >= 0x1D400 and <= 0x1D6A3)
        {
            if (IsHole(cp))
            {
                ascii = '\0';
                style = default!;
                return false;
            }

            int offset = cp - 0x1D400;
            int alphabet = offset / 52;
            int letter = offset % 52;
            ascii = (char)(letter < 26 ? 'A' + letter : 'a' + (letter - 26));

            style = alphabet switch
            {
                0 => BoldRoman,
                1 => ItalicRoman,
                2 => BoldItalicRoman,
                3 => ScriptItalic,
                4 => BoldScript,
                5 => FrakturItalic,
                6 => DoubleStruckItalic,
                7 => BoldFraktur,
                8 => SansSerifItalic,
                9 => SansSerifBold,
                10 => SansSerifItalic,
                11 => SansSerifBoldItalic,
                _ => MonospaceItalic,
            };
            return true;
        }

        // 3. Mathematical Alphanumeric Symbols: Digits (U+1D7CE..U+1D7FF)
        if (cp is >= 0x1D7CE and <= 0x1D7FF)
        {
            int offset = cp - 0x1D7CE;
            int group = offset / 10;
            int digit = offset % 10;
            ascii = (char)('0' + digit);

            style = group switch
            {
                0 => BoldRoman,
                1 => DoubleStruckItalic,
                2 => SansSerifItalic,
                3 => SansSerifBold,
                _ => MonospaceItalic,
            };
            return true;
        }

        ascii = '\0';
        style = default!;
        return false;
    }
}
