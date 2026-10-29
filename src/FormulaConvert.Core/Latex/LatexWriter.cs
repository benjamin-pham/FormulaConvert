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
                WriteItems(Flatten(row), b);
                break;
            case Run run:
                WriteStyledRuns([run], b);
                break;
            case Frac f:
                WriteFrac(f, b);
                break;
            case Delimited d when d.IsBinomial(out var top, out var bottom):
                WriteBinomial(top, bottom, b);
                break;
            case Delimited d when d.IsMatrixEnvironment(out var envName, out var envMatrix):
                WriteMatrix(envName, envMatrix, b);
                break;
            case Delimited d when d.IsCases(out var casesName, out var casesRows):
                WriteEquations(casesName, casesRows, b);
                break;
            case Delimited d:
                WriteDelimited(d, b);
                break;
            case Script s:
                WriteScript(s, b);
                break;
            case PreScript s:
                WritePreScript(s, b);
                break;
            case Radical r:
                WriteRadical(r, b);
                break;
            case Nary n:
                WriteNary(n, b);
                break;
            case FunctionApply f:
                WriteFunctionApply(f, b);
                break;
            case Matrix m:
                WriteMatrix(m.ColumnAlign == null ? "matrix" : "array", m, b);
                break;
            case EqArray e:
                WriteEquations(HasAlignmentMark(e) ? "align" : "gathered", e, b);
                break;
            case Accent a:
                WriteAccent(a, b);
                break;
            case Bar bar:
                WriteBar(bar, b);
                break;
            case GroupChar g:
                WriteGroupChar(g, b);
                break;
            case Limit l:
                WriteLimit(l, b);
                break;
            case BorderBox box:
                WriteBorderBox(box, b);
                break;
            case Phantom ph:
                WritePhantom(ph, b);
                break;
            // Feature slices add cases here, e.g. `case Frac f: WriteFrac(f, b); break;`
            default:
                throw new NotSupportedException($"LatexWriter does not support {node.GetType().Name} nodes.");
        }
        b.Nesting.Exit();
    }

    /// <summary>One character: printable ASCII literally (with escapes), others via the canonical symbol command.</summary>
    internal static void WriteChar(Rune rune, LatexBuilder b)
    {
        int cp = rune.Value;
        if (cp < 0x80)
        {
            char c = (char)cp;
            if (c == '\\') b.Command("\\backslash");
            else if (c is '^' or '~' or '\'') b.HexChar(cp);
            else if (EscapedChars.Contains(c)) b.Command("\\" + c);
            else b.Text(c.ToString());
        }
        else if (NaryTable.TryGetByChar(rune.ToString(), out _))
        {
            b.Text(rune.ToString()); // a command would re-parse as an operator that swallows the next atom
        }
        else if (SymbolTable.TryGetCommand(cp, out var command))
        {
            b.Command(command);
        }
        else if (SymbolTable.TryGetParseOnlyKind(cp, out var kind))
        {
            if (kind == "rel")
            {
                b.Command("\\mathrel");
                b.Text("{" + rune.ToString() + "}");
            }
            else if (kind == "bin")
            {
                b.Command("\\mathbin");
                b.Text("{" + rune.ToString() + "}");
            }
            else
            {
                b.Text(rune.ToString());
            }
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
    private enum Pending { None, Letter, LetterOrDigit, HexDigit }

    private readonly StringBuilder _sb = new();
    private Pending _pending;

    public LatexBuilder(NestingGuard? nesting = null) => Nesting = nesting ?? new NestingGuard(NestingGuard.XmlLimit);

    /// <summary>Depth guard for <see cref="LatexWriter.WriteNode"/> recursion (shared with nested builders).</summary>
    public NestingGuard Nesting { get; }

    /// <summary>Appends a control sequence such as <c>\alpha</c> or <c>\{</c>.</summary>
    public LatexBuilder Command(string command)
    {
        Emit(command);
        _pending = command.Length > 1 && LatexTokenizer.IsAsciiLetter(command[^1]) ? Pending.Letter : Pending.None;
        return this;
    }

    /// <summary>Appends <c>\char"HH</c>; a following hexadecimal digit is separated by a space so it is not absorbed.</summary>
    public void HexChar(int codePoint)
    {
        Emit("\\char\"" + codePoint.ToString("X2"));
        _pending = Pending.HexDigit;
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
            Pending.HexDigit => char.IsAsciiHexDigit(first),
            Pending.LetterOrDigit => LatexTokenizer.IsAsciiLetter(first) || char.IsAsciiDigit(first),
            _ => false,
        };
        if (space) _sb.Append(' ');
        _sb.Append(s);
    }

    public override string ToString() => _sb.ToString();
}
