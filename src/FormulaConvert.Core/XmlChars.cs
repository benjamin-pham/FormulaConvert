namespace FormulaConvert;

internal static class XmlChars
{
    /// <summary>True when the code point matches the XML 1.0 <c>Char</c> production.</summary>
    public static bool IsValid(int cp) =>
        cp is 0x9 or 0xA or 0xD
            or >= 0x20 and <= 0xD7FF
            or >= 0xE000 and <= 0xFFFD
            or >= 0x10000 and <= 0x10FFFF;
}
