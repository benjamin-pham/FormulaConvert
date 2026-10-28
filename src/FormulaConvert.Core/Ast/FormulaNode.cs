namespace FormulaConvert.Ast;

public enum Sty { Italic, Plain, Bold, BoldItalic }

public enum Scr { Roman, Script, Fraktur, DoubleStruck, SansSerif, Monospace }

public enum FracKind { Bar, NoBar, Lin }

[Flags]
public enum BoxEdges
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
    StrikeH = 16,
    StrikeV = 32,
    StrikeUp = 64,
    StrikeDown = 128,
}

public abstract record FormulaNode;

public sealed record RunStyle(Sty Sty = Sty.Italic, Scr Scr = Scr.Roman, bool Normal = false);

public sealed record FormulaRow(IReadOnlyList<FormulaNode> Items) : FormulaNode
{
    public static FormulaRow Empty { get; } = new FormulaRow(Array.Empty<FormulaNode>());
}

public sealed record Run(string Text, RunStyle Style) : FormulaNode;

public sealed record Frac(FormulaNode Num, FormulaNode Den, FracKind Kind) : FormulaNode;

public sealed record Radical(FormulaNode? Degree, FormulaNode Body) : FormulaNode;

public sealed record Script(FormulaNode Base, FormulaNode? Sub, FormulaNode? Sup) : FormulaNode;

public sealed record PreScript(FormulaNode Base, FormulaNode? Sub, FormulaNode? Sup) : FormulaNode;

public sealed record Nary(string Chr, FormulaNode? Sub, FormulaNode? Sup, FormulaNode Body, bool SubSupLimits) : FormulaNode;

/// <summary>Brackets around one or more parts; <c>""</c> for Open/Close/Separator means absent.</summary>
public sealed record Delimited(string Open, string Close, string Separator, IReadOnlyList<FormulaNode> Parts) : FormulaNode
{
    /// <summary>The <c>\binom</c> shape: default parentheses around one bar-less fraction.</summary>
    public static Delimited Binomial(FormulaNode top, FormulaNode bottom) =>
        new("(", ")", "", [new Frac(top, bottom, FracKind.NoBar)]);

    /// <summary>True for the <see cref="Binomial"/> shape (default parentheses, no separator, one bar-less fraction).</summary>
    public bool IsBinomial(out FormulaNode top, out FormulaNode bottom)
    {
        if (this is { Open: "(", Close: ")", Separator: "", Parts: [Frac { Kind: FracKind.NoBar } f] })
        {
            top = f.Num;
            bottom = f.Den;
            return true;
        }
        top = bottom = FormulaRow.Empty;
        return false;
    }

    private static readonly (string Name, string Open, string Close)[] MatrixBrackets =
    [
        ("pmatrix", "(", ")"),
        ("bmatrix", "[", "]"),
        ("Bmatrix", "{", "}"),
        ("vmatrix", "|", "|"),
        ("Vmatrix", "‖", "‖"),
    ];

    /// <summary>The bracketed matrix environment <paramref name="name"/> (<c>pmatrix</c>, <c>bmatrix</c>, <c>Bmatrix</c>, <c>vmatrix</c>, <c>Vmatrix</c>).</summary>
    public static Delimited OfMatrix(string name, Matrix matrix)
    {
        foreach (var (n, open, close) in MatrixBrackets)
            if (n == name) return new(open, close, "", [matrix]);
        throw new ArgumentException($"unknown matrix environment {name}", nameof(name));
    }

    /// <summary>True for a bracketed matrix environment: one <see cref="Matrix"/> without column alignment, no separator, and the exact bracket pair of that environment.</summary>
    public bool IsMatrixEnvironment(out string name, out Matrix matrix)
    {
        if (this is { Separator: "", Parts: [Matrix { ColumnAlign: null } m] })
        {
            foreach (var (n, open, close) in MatrixBrackets)
            {
                if (Open != open || Close != close) continue;
                name = n;
                matrix = m;
                return true;
            }
        }
        name = "";
        matrix = null!;
        return false;
    }

    /// <summary><c>cases</c> (<c>{</c> only) or <c>rcases</c> (<c>}</c> only) around an equation array.</summary>
    public static Delimited OfCases(string name, EqArray rows) => name switch
    {
        "cases" => new("{", "", "", [rows]),
        "rcases" => new("", "}", "", [rows]),
        _ => throw new ArgumentException($"unknown cases environment {name}", nameof(name)),
    };

    /// <summary>True for the <see cref="OfCases"/> shapes: one <see cref="EqArray"/>, no separator, and a brace on exactly one side.</summary>
    public bool IsCases(out string name, out EqArray rows)
    {
        if (this is { Separator: "", Parts: [EqArray e] } && (Open, Close) is ("{", "") or ("", "}"))
        {
            name = Open == "{" ? "cases" : "rcases";
            rows = e;
            return true;
        }
        name = "";
        rows = null!;
        return false;
    }
}

/// <summary>Rows of cells. <c>ColumnAlign</c> is one of <c>l c r</c> per column with trailing <c>c</c> trimmed, or null when every column is centered.</summary>
public sealed record Matrix(IReadOnlyList<IReadOnlyList<FormulaNode>> Rows, string? ColumnAlign) : FormulaNode;

public sealed record EqArray(IReadOnlyList<FormulaNode> Rows) : FormulaNode;

public sealed record Accent(string Chr, FormulaNode Body) : FormulaNode;

public sealed record Bar(bool Top, FormulaNode Body) : FormulaNode;

public sealed record GroupChar(string Chr, bool Top, FormulaNode Body) : FormulaNode;

public sealed record Limit(FormulaNode Base, FormulaNode Lim, bool Upper) : FormulaNode;

public sealed record FunctionApply(FormulaNode Name, FormulaNode Arg) : FormulaNode;

public sealed record BorderBox(BoxEdges Edges, FormulaNode Body) : FormulaNode;

public sealed record Phantom(bool Show, bool ZeroWidth, bool ZeroAscent, bool ZeroDescent, FormulaNode Body) : FormulaNode;
