using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// <c>\left … \middle … \right</c> and the sized delimiters (<c>\bigl \Bigr \bigg …</c>). The row inside
/// <c>\left</c> is parsed in <see cref="RowContext.Left"/>, so <c>\middle</c> and <c>\right</c> end it.
/// </summary>
internal static class DelimiterCommands
{
    private const string LiteralDelimiters = "()[]|/<>";

    public static void Register(CommandTable t)
    {
        t.Add("\\left", Left);
        t.Add("\\mleft", Left);
        t.Add("\\right", (p, name) => throw p.Error("\\right without matching \\left", p.Peek(-1).Position));
        t.Add("\\mright", (p, name) => throw p.Error("\\mright without matching \\mleft", p.Peek(-1).Position));
        t.Add("\\middle", (p, name) => throw p.Error("\\middle outside \\left … \\right", p.Peek(-1).Position));
        foreach (var size in new[] { "big", "Big", "bigg", "Bigg" })
        {
            t.Add("\\" + size, Sized);
            foreach (var side in new[] { "l", "r", "m" })
                t.Add("\\" + size + side, Sized);
        }
    }

    private static FormulaNode Left(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position;
        var open = ReadDelimiter(p, name);
        var parts = new List<FormulaNode>();
        string? separator = null;
        p.EnterNesting();
        p.PushContext(RowContext.Left);
        string close;
        while (true)
        {
            parts.Add(p.ParseRowRest());
            var t = p.Peek();
            if (t.Kind == TokenKind.Command && t.Text == "\\middle")
            {
                p.Next();
                var middle = ReadDelimiter(p, t.Text);
                if (separator == null) separator = middle;
                else if (separator != middle)
                    p.Warn("lossy", $"\\middle delimiters differ; only the first ({Show(separator)}) is kept", t.Position);
                continue;
            }
            if (t.Kind == TokenKind.Command && t.Text is "\\right" or "\\mright")
            {
                p.Next();
                close = ReadDelimiter(p, t.Text);
                break;
            }
            throw p.Error(name == "\\mleft" ? "\\mleft without matching \\mright" : "\\left without matching \\right", at);
        }
        p.PopContext();
        p.ExitNesting();
        return new Delimited(open, close, separator ?? "", parts);
    }

    /// <summary>A sized delimiter has no OMML counterpart: the bare delimiter character is kept, with a warning.</summary>
    private static FormulaNode Sized(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position;
        var chr = ReadDelimiter(p, name);
        p.Warn("lossy", $"{name} size is dropped (OMML delimiters have no explicit size)", at);
        return chr.Length == 0 ? FormulaRow.Empty : LatexParser.CharRun(chr);
    }

    private static string Show(string delimiter) => delimiter.Length == 0 ? "." : delimiter;

    /// <summary>
    /// The delimiter after <c>\left</c>/<c>\middle</c>/<c>\right</c>/<c>\big…</c>: a literal character, <c>.</c> (absent, as
    /// an empty string), <c>\{ \} \|</c>, or a symbol command. Normalizes vertical-bar forms to <c>|</c> and U+2016.
    /// </summary>
    internal static string ReadDelimiter(LatexParser p, string after)
    {
        var t = p.Peek();
        string? result = null;
        switch (t.Kind)
        {
            case TokenKind.LBracket or TokenKind.RBracket:
                result = t.Text;
                break;
            case TokenKind.Char when t.Text == ".":
                result = "";
                break;
            case TokenKind.Char when t.Text.Length == 1 && LiteralDelimiters.Contains(t.Text[0]):
                result = t.Text;
                break;
            case TokenKind.Char when char.ConvertToUtf32(t.Text, 0) >= 0x80:
                result = Normalize(char.ConvertToUtf32(t.Text, 0));
                break;
            case TokenKind.Command when t.Text == "\\{":
                result = "{";
                break;
            case TokenKind.Command when t.Text == "\\}":
                result = "}";
                break;
            case TokenKind.Command when t.Text == "\\|":
                result = "‖";
                break;
            case TokenKind.Command when SymbolTable.TryGetCodepoint(t.Text, out var cp):
                result = Normalize(cp);
                break;
        }
        if (result == null)
            throw p.Error(t.Kind == TokenKind.EndOfInput ? $"missing delimiter after {after}" : $"invalid delimiter {t.Text} after {after}", t.Position);
        p.Next();
        return result;
    }

    /// <summary><c>∣</c> (U+2223) is the vertical bar; <c>∥</c> (U+2225) is the double bar U+2016.</summary>
    internal static string Normalize(int codepoint) => codepoint switch
    {
        0x2223 => "|",
        0x2225 => "‖",
        _ => char.ConvertFromUtf32(codepoint),
    };
}
