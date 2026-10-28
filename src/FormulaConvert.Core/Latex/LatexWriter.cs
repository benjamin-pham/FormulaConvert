using System.Text;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex;

/// <summary>
/// Writes the AST as canonical LaTeX (see "Quy ước" in the plan). Node kinds are dispatched in
/// <see cref="WriteNode"/>; feature slices add a case there and implement it in <c>LatexWriter.&lt;Group&gt;.cs</c>.
/// </summary>
internal static partial class LatexWriter
{
    private const string EscapedChars = "#$%&_{}";

    public static string Write(FormulaNode root)
    {
        var b = new LatexBuilder();
        WriteNode(root, b);
        return b.ToString();
    }

    internal static void WriteNode(FormulaNode node, LatexBuilder b)
    {
        b.Nesting.Enter(-1);
        switch (node)
        {
            case FormulaRow row:
                foreach (var item in row.Items) WriteNode(item, b);
                break;
            case Run run:
                WriteRun(run, b);
                break;
            case Frac f:
                WriteFrac(f, b);
                break;
            case Delimited d when IsBinomial(d):
                WriteBinomial(d, b);
                break;
            // Feature slices add cases here, e.g. `case Frac f: WriteFrac(f, b); break;`
            default:
                throw new NotSupportedException($"LatexWriter does not support {node.GetType().Name} nodes.");
        }
        b.Nesting.Exit();
    }

    private static void WriteRun(Run run, LatexBuilder b)
    {
        // Styles (\mathrm, \mathbf, …) are added by the style slice.
        foreach (var rune in run.Text.EnumerateRunes())
            WriteChar(rune, b);
    }

    /// <summary>One character: printable ASCII literally (with escapes), others via the canonical symbol command.</summary>
    internal static void WriteChar(Rune rune, LatexBuilder b)
    {
        int cp = rune.Value;
        if (cp < 0x80)
        {
            char c = (char)cp;
            if (c == '\\') b.Command("\\backslash");
            else if (EscapedChars.Contains(c)) b.Command("\\" + c);
            else b.Text(c.ToString());
        }
        else if (SymbolTable.TryGetCommand(cp, out var command))
        {
            b.Command(command);
        }
        else
        {
            b.Text(rune.ToString());
        }
    }
}

/// <summary>
/// Accumulates LaTeX output and inserts the canonical separating spaces: after a command ending in a letter
/// when the next character is an ASCII letter, and (after <see cref="AfterUnbracedScript"/>) when the next
/// character is an ASCII letter or digit.
/// </summary>
internal sealed class LatexBuilder
{
    private enum Pending { None, Letter, LetterOrDigit }

    private readonly StringBuilder _sb = new();
    private Pending _pending;

    /// <summary>Depth guard for <see cref="LatexWriter.WriteNode"/> recursion.</summary>
    public NestingGuard Nesting { get; } = new();

    /// <summary>Appends a control sequence such as <c>\alpha</c> or <c>\{</c>.</summary>
    public LatexBuilder Command(string command)
    {
        Emit(command);
        _pending = command.Length > 1 && LatexTokenizer.IsAsciiLetter(command[^1]) ? Pending.Letter : Pending.None;
        return this;
    }

    /// <summary>Appends literal text (characters, braces, operators).</summary>
    public LatexBuilder Text(string text)
    {
        if (text.Length == 0) return this;
        Emit(text);
        _pending = Pending.None;
        return this;
    }

    /// <summary>Call after writing a script argument without braces (<c>x^2</c>, <c>\log_2</c>).</summary>
    public void AfterUnbracedScript() => _pending = Pending.LetterOrDigit;

    private void Emit(string s)
    {
        char first = s[0];
        bool space = _pending switch
        {
            Pending.Letter => LatexTokenizer.IsAsciiLetter(first),
            Pending.LetterOrDigit => LatexTokenizer.IsAsciiLetter(first) || char.IsAsciiDigit(first),
            _ => false,
        };
        if (space) _sb.Append(' ');
        _sb.Append(s);
    }

    public override string ToString() => _sb.ToString();
}
