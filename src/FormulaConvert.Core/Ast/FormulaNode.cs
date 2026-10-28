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

public sealed record Delimited(string Open, string Close, string Separator, IReadOnlyList<FormulaNode> Parts) : FormulaNode;

public sealed record Matrix(IReadOnlyList<IReadOnlyList<FormulaNode>> Rows, string? ColumnAlign) : FormulaNode;

public sealed record EqArray(IReadOnlyList<FormulaNode> Rows) : FormulaNode;

public sealed record Accent(string Chr, FormulaNode Body) : FormulaNode;

public sealed record Bar(bool Top, FormulaNode Body) : FormulaNode;

public sealed record GroupChar(string Chr, bool Top, FormulaNode Body) : FormulaNode;

public sealed record Limit(FormulaNode Base, FormulaNode Lim, bool Upper) : FormulaNode;

public sealed record FunctionApply(FormulaNode Name, FormulaNode Arg) : FormulaNode;

public sealed record BorderBox(BoxEdges Edges, FormulaNode Body) : FormulaNode;

public sealed record Phantom(bool Show, bool ZeroWidth, bool ZeroAscent, bool ZeroDescent, FormulaNode Body) : FormulaNode;
