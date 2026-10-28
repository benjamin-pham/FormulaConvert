using System.Text;

namespace FormulaConvert.Latex;

internal enum TokenKind
{
    /// <summary>One ordinary character (a full code point, surrogate pairs kept together).</summary>
    Char,
    /// <summary><c>\name</c> (letters) or <c>\x</c> (one non-letter character); <see cref="Token.Text"/> includes the backslash.</summary>
    Command,
    BeginGroup,
    EndGroup,
    LBracket,
    RBracket,
    Superscript,
    Subscript,
    Ampersand,
    Prime,
    /// <summary><c>\\</c></summary>
    NewLine,
    EndOfInput,
}

/// <summary>A token with its source span <c>[Position, End)</c>.</summary>
internal readonly record struct Token(TokenKind Kind, string Text, int Position, int End);

/// <summary>
/// Splits LaTeX into tokens. Whitespace and <c>%</c> comments are dropped; <c>^^hh</c> and <c>^^^^hhhh</c>
/// become <see cref="TokenKind.Char"/> tokens. The list always ends with one <see cref="TokenKind.EndOfInput"/>.
/// </summary>
internal static class LatexTokenizer
{
    public static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            int start = i;
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            switch (c)
            {
                case '%':
                    while (i < s.Length && s[i] != '\n' && s[i] != '\r') i++;
                    continue;
                case '\\':
                    tokens.Add(ReadCommand(s, ref i));
                    continue;
                case '{': tokens.Add(new Token(TokenKind.BeginGroup, "{", start, ++i)); continue;
                case '}': tokens.Add(new Token(TokenKind.EndGroup, "}", start, ++i)); continue;
                case '[': tokens.Add(new Token(TokenKind.LBracket, "[", start, ++i)); continue;
                case ']': tokens.Add(new Token(TokenKind.RBracket, "]", start, ++i)); continue;
                case '_': tokens.Add(new Token(TokenKind.Subscript, "_", start, ++i)); continue;
                case '&': tokens.Add(new Token(TokenKind.Ampersand, "&", start, ++i)); continue;
                case '\'': tokens.Add(new Token(TokenKind.Prime, "'", start, ++i)); continue;
                case '^':
                    if (TryReadCaretNotation(s, ref i, out var caret))
                        tokens.Add(caret);
                    else
                        tokens.Add(new Token(TokenKind.Superscript, "^", start, ++i));
                    continue;
            }
            var rune = ReadRune(s, i);
            i += rune.Length;
            tokens.Add(new Token(TokenKind.Char, rune, start, i));
        }
        tokens.Add(new Token(TokenKind.EndOfInput, "", s.Length, s.Length));
        return tokens;
    }

    private static Token ReadCommand(string s, ref int i)
    {
        int start = i++;
        if (i >= s.Length)
            throw new FormulaParseException("unexpected end of input after \\", start);
        if (s[i] == '\\')
        {
            i++;
            return new Token(TokenKind.NewLine, "\\\\", start, i);
        }
        if (IsAsciiLetter(s[i]))
        {
            while (i < s.Length && IsAsciiLetter(s[i])) i++;
        }
        else
        {
            i += ReadRune(s, i).Length;
        }
        return new Token(TokenKind.Command, s[start..i], start, i);
    }

    /// <summary><c>^^^^hhhh</c> (four hex digits) or <c>^^hh</c> (two hex digits).</summary>
    private static bool TryReadCaretNotation(string s, ref int i, out Token token)
    {
        int start = i;
        token = default;
        foreach (var carets in new[] { 4, 2 })
        {
            if (HasCarets(s, start, carets) && HasHex(s, start + carets, carets))
            {
                var cp = int.Parse(s.AsSpan(start + carets, carets), System.Globalization.NumberStyles.HexNumber);
                if (!XmlChars.IsValid(cp))
                    throw new FormulaParseException($"invalid character code {s.Substring(start, 2 * carets)}", start);
                i = start + 2 * carets;
                token = new Token(TokenKind.Char, char.ConvertFromUtf32(cp), start, i);
                return true;
            }
        }
        return false;
    }

    private static bool HasCarets(string s, int at, int n)
    {
        if (at + n > s.Length) return false;
        for (int k = 0; k < n; k++)
            if (s[at + k] != '^') return false;
        return true;
    }

    private static bool HasHex(string s, int at, int n)
    {
        if (at + n > s.Length) return false;
        for (int k = 0; k < n; k++)
            if (!char.IsAsciiHexDigit(s[at + k])) return false;
        return true;
    }

    /// <summary>One code point at <paramref name="i"/>; unpaired surrogates and characters XML cannot hold throw.</summary>
    private static string ReadRune(string s, int i)
    {
        if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            return s.Substring(i, 2);
        if (char.IsSurrogate(s[i]))
            throw new FormulaParseException($"unpaired surrogate U+{(int)s[i]:X4}", i);
        if (!XmlChars.IsValid(s[i]))
            throw new FormulaParseException($"invalid character U+{(int)s[i]:X4}", i);
        return s[i].ToString();
    }

    internal static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
}
