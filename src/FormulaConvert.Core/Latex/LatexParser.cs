using System.Text;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex;

/// <summary>
/// Recursive-descent LaTeX parser producing the <see cref="FormulaNode"/> AST. One <see cref="Run"/> per character.
/// Command handlers (see <see cref="CommandTable"/>) use the public members to read their arguments.
/// </summary>
internal sealed partial class LatexParser
{
    private const int MaxAliasDepth = 32;

    private readonly string _source;
    private readonly List<Token> _tokens;
    private readonly CommandTable _commands;
    private readonly int? _reportedPosition;
    private readonly int _aliasDepth;
    private int _index;

    /// <summary>One entry per open <c>{</c> (false) or optional <c>[</c> (true); decides whether <c>]</c> ends a row.</summary>
    private readonly Stack<bool> _contexts = new();

    private LatexParser(string latex, List<Diagnostic> diagnostics, CommandTable commands, int? reportedPosition, int aliasDepth)
    {
        _source = latex;
        _commands = commands;
        _reportedPosition = reportedPosition;
        _aliasDepth = aliasDepth;
        Diagnostics = diagnostics;
        try
        {
            _tokens = LatexTokenizer.Tokenize(latex);
        }
        catch (FormulaParseException ex) when (reportedPosition is int p)
        {
            throw new FormulaParseException(ex.Message, p);
        }
    }

    /// <summary>Parses a complete LaTeX formula; throws <see cref="FormulaParseException"/> on syntax errors.</summary>
    public static FormulaNode Parse(string latex, List<Diagnostic> diagnostics) =>
        new LatexParser(latex, diagnostics, CommandTable.Default, null, 0).ParseAll();

    public List<Diagnostic> Diagnostics { get; }

    /// <summary>Source position of the next unread token (or of the expanded alias command, inside an alias expansion).</summary>
    public int Position => _reportedPosition ?? Peek().Position;

    /// <summary>The LaTeX source being parsed.</summary>
    public string Source => _source;

    public Token Peek(int offset = 0) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

    public Token Next()
    {
        var t = Peek();
        if (_index < _tokens.Count - 1) _index++;
        return t;
    }

    public bool AtEnd => Peek().Kind == TokenKind.EndOfInput;

    /// <summary>An exception at <see cref="Position"/>; use as <c>throw p.Error("…")</c>.</summary>
    public FormulaParseException Error(string message) => new(message, Position);

    /// <summary>An exception at a given source position (still remapped inside alias expansions).</summary>
    public FormulaParseException Error(string message, int position) => new(message, _reportedPosition ?? position);

    /// <summary>Adds a warning at <see cref="Position"/> (or at <paramref name="position"/>).</summary>
    public void Warn(string code, string message, int? position = null) =>
        Diagnostics.Add(new Diagnostic(code, message, _reportedPosition ?? position ?? Position));

    /// <summary>A default-style <see cref="Run"/> for one character.</summary>
    public static Run CharRun(string text) => new(text, new RunStyle());

    /// <summary>0 items → <see cref="FormulaRow.Empty"/>, 1 item → that item, otherwise a <see cref="FormulaRow"/>.</summary>
    public static FormulaNode Rowify(List<FormulaNode> items) => items.Count switch
    {
        0 => FormulaRow.Empty,
        1 => items[0],
        _ => new FormulaRow(items),
    };

    private FormulaNode ParseAll()
    {
        var node = ParseRowRest();
        if (!AtEnd)
            throw Error($"unexpected {Describe(Peek())}");
        return node;
    }

    /// <summary>Reads items until the end of the current row (<c>}</c>, end of input, or another row terminator).</summary>
    public FormulaNode ParseRowRest()
    {
        var items = new List<FormulaNode>();
        while (!IsRowTerminator(Peek()))
        {
            var t = Peek();
            if (t.Kind == TokenKind.Command && _commands.TryGetInfix(t.Text, out var infix))
            {
                Next();
                var result = infix(this, t.Text, items);
                items = [result];
                continue;
            }
            items.Add(ParsePostfix(ParseAtom()));
        }
        return Rowify(items);
    }

    /// <summary>Tokens that end a row. Later contexts add <c>\right</c>, <c>\end</c>, <c>&amp;</c>, <c>\\</c>.</summary>
    private bool IsRowTerminator(Token t) => t.Kind switch
    {
        TokenKind.EndOfInput or TokenKind.EndGroup => true,
        TokenKind.RBracket => _contexts.Count > 0 && _contexts.Peek(),
        _ => false,
    };

    /// <summary>Scripts and primes attached after an atom (none yet).</summary>
    private FormulaNode ParsePostfix(FormulaNode atom) => atom;

    /// <summary>A required argument: a <c>{…}</c> group or a single atom.</summary>
    public FormulaNode ReadArgument()
    {
        var t = Peek();
        if (IsRowTerminator(t))
            throw Error("missing argument");
        return t.Kind == TokenKind.BeginGroup ? ParseGroup() : ParseAtom();
    }

    /// <summary>An optional <c>[…]</c> argument, or null when the next token is not <c>[</c>.</summary>
    public FormulaNode? ReadOptionalArgument()
    {
        var open = Peek();
        if (open.Kind != TokenKind.LBracket) return null;
        Next();
        _contexts.Push(true);
        var node = ParseRowRest();
        _contexts.Pop();
        if (Peek().Kind != TokenKind.RBracket)
            throw Error("unclosed [", open.Position);
        Next();
        return node;
    }

    /// <summary>The raw source text between <c>{</c> and its matching <c>}</c> (whitespace and comments included).</summary>
    public string ReadRawGroup()
    {
        var open = Peek();
        if (open.Kind != TokenKind.BeginGroup)
            throw Error("expected {");
        int depth = 0;
        for (int k = _index; k < _tokens.Count; k++)
        {
            var t = _tokens[k];
            if (t.Kind == TokenKind.BeginGroup) depth++;
            else if (t.Kind == TokenKind.EndGroup && --depth == 0)
            {
                _index = k + 1;
                return _source[open.End..t.Position];
            }
        }
        throw Error("unclosed {", open.Position);
    }

    private FormulaNode ParseGroup()
    {
        var open = Next();
        _contexts.Push(false);
        var node = ParseRowRest();
        _contexts.Pop();
        if (Peek().Kind != TokenKind.EndGroup)
            throw Error("unclosed {", open.Position);
        Next();
        return node;
    }

    /// <summary>One atom: a group, a character, or a command.</summary>
    private FormulaNode ParseAtom()
    {
        var t = Peek();
        switch (t.Kind)
        {
            case TokenKind.BeginGroup:
                return ParseGroup();
            case TokenKind.Char:
            case TokenKind.LBracket:
            case TokenKind.RBracket:
                Next();
                return CharRun(t.Text);
            case TokenKind.Command:
                return ParseCommand();
            default:
                throw Error($"unexpected {Describe(t)}");
        }
    }

    private FormulaNode ParseCommand()
    {
        var t = Peek();
        var name = t.Text;
        if (_commands.TryGet(name, out var handler))
        {
            Next();
            return handler(this, name);
        }
        if (AliasTable.TryGet(name, out var alias))
        {
            Next();
            return ExpandAlias(t, alias.Arity, alias.Replacement);
        }
        if (SymbolTable.TryGetCodepoint(name, out var cp))
        {
            Next();
            return CharRun(char.ConvertFromUtf32(cp));
        }
        if (_commands.TryGetInfix(name, out _))
            throw Error($"misplaced {name}");
        throw Error($"unknown command {name}");
    }

    private FormulaNode ExpandAlias(Token command, int arity, string replacement)
    {
        if (_aliasDepth >= MaxAliasDepth)
            throw Error($"alias expansion too deep at {command.Text}", command.Position);
        var text = new StringBuilder(replacement);
        for (int n = 1; n <= arity; n++)
        {
            var arg = Peek().Kind == TokenKind.BeginGroup ? ReadRawGroup() : ReadRawToken();
            text.Replace("#" + n, arg);
        }
        var sub = new LatexParser(text.ToString(), Diagnostics, _commands, _reportedPosition ?? command.Position, _aliasDepth + 1);
        return sub.ParseAll();
    }

    private string ReadRawToken()
    {
        var t = Peek();
        if (IsRowTerminator(t))
            throw Error("missing argument");
        Next();
        return _source[t.Position..t.End];
    }

    private static string Describe(Token t) => t.Kind == TokenKind.EndOfInput ? "end of input" : t.Text;
}
