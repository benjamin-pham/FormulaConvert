using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// <c>\begin{name} … \end{name}</c>: matrices (<c>matrix pmatrix … array</c>), <c>cases</c>, the align family and
/// transparent wrappers (<c>equation</c> …). Cells end at <c>&amp;</c>, <c>\\</c> or <c>\end</c> (<see cref="RowContext.Cell"/>);
/// in equation arrays <c>&amp;</c> is kept as a literal <c>Run("&amp;")</c> (<see cref="RowContext.Line"/>).
/// </summary>
internal static class EnvironmentCommands
{
    private static readonly string[] BracketedMatrices = ["pmatrix", "bmatrix", "Bmatrix", "vmatrix", "Vmatrix"];
    private static readonly string[] Transparent = ["equation", "math", "displaymath", "subequations", "eqnarray", "center"];

    public static void Register(CommandTable t)
    {
        t.Add("\\begin", Begin);
        t.Add("\\end", (p, name) => throw p.Error("\\end without matching \\begin", p.Peek(-1).Position));
    }

    private static FormulaNode Begin(LatexParser p, string command)
    {
        int at = p.Peek(-1).Position;
        var name = ReadName(p);
        p.EnterNesting();
        var node = ParseEnvironment(p, name, at);
        p.ExitNesting();
        return node;
    }

    private static FormulaNode ParseEnvironment(LatexParser p, string name, int at)
    {
        var baseName = name.EndsWith('*') ? name[..^1] : name;
        bool starred = baseName.Length != name.Length;
        switch (baseName)
        {
            case "matrix" or "smallmatrix" or "pmatrix" or "bmatrix" or "Bmatrix" or "vmatrix" or "Vmatrix":
            {
                string? align = null;
                if (starred && p.ReadRawOptional() is { } spec) align = ColumnAlign(p, spec, at);
                if (baseName == "smallmatrix")
                    p.Warn("lossy", "smallmatrix is read as matrix (OMML has no small matrices)", at);
                var matrix = ParseMatrix(p, name, at, align);
                return Array.IndexOf(BracketedMatrices, baseName) >= 0 ? Delimited.OfMatrix(baseName, matrix) : matrix;
            }
            case "array" when !starred:
            {
                if (p.Peek().Kind != TokenKind.BeginGroup)
                    throw p.Error("\\begin{array} requires a column specification");
                var align = ColumnAlign(p, p.ReadRawGroup(), at);
                return ParseMatrix(p, name, at, align);
            }
            case "cases" or "rcases" or "dcases" when !starred:
            {
                if (baseName == "dcases")
                    p.Warn("lossy", "dcases is read as cases (OMML has no display-style cases)", at);
                var rows = ParseEquations(p, name, at);
                return Delimited.OfCases(baseName == "rcases" ? "rcases" : "cases", rows);
            }
            case "align" or "aligned" or "split" or "gather" or "gathered" or "multline" when !starred || baseName is "align" or "gather" or "multline":
            {
                if (baseName == "multline")
                    p.Warn("lossy", "multline is read as gathered (OMML has no multline layout)", at);
                return ParseEquations(p, name, at);
            }
            case "equation" or "math" or "displaymath" or "subequations" or "eqnarray" or "center" when !starred || baseName == "equation":
            {
                p.PushContext(RowContext.Body);
                var body = p.ParseRowRest();
                p.PopContext();
                ReadEnd(p, name, at);
                return body;
            }
        }
        throw p.Error($"unknown environment {name}", at);
    }

    /// <summary>The environment name from <c>{name}</c>, whitespace removed.</summary>
    private static string ReadName(LatexParser p)
    {
        var raw = p.ReadRawGroup();
        return string.Concat(raw.Where(c => !char.IsWhiteSpace(c)));
    }

    private static Matrix ParseMatrix(LatexParser p, string name, int at, string? align)
    {
        var rows = ParseRows(p, RowContext.Cell, name, at);
        int width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        foreach (var row in rows)
            while (row.Count < width) row.Add(FormulaRow.Empty);
        return new Matrix(rows.Select(r => (IReadOnlyList<FormulaNode>)r).ToList(), align);
    }

    private static EqArray ParseEquations(LatexParser p, string name, int at) =>
        new(ParseRows(p, RowContext.Line, name, at).Select(r => r[0]).ToList());

    /// <summary>
    /// Rows separated by <c>\\</c>, each a list of cells; reads the closing <c>\end{name}</c>. An empty row right
    /// before <c>\end</c> (trailing <c>\\</c>) is dropped.
    /// </summary>
    private static List<List<FormulaNode>> ParseRows(LatexParser p, RowContext context, string name, int at)
    {
        var rows = new List<List<FormulaNode>>();
        p.PushContext(context);
        bool more = true;
        while (more && !IsEnd(p.Peek()))
        {
            var row = new List<FormulaNode>();
            while (true)
            {
                row.Add(p.ParseRowRest());
                var t = p.Peek();
                if (t.Kind == TokenKind.Ampersand)
                {
                    p.Next();
                    continue;
                }
                if (t.Kind == TokenKind.NewLine) p.Next();
                else more = false;
                break;
            }
            rows.Add(row);
        }
        p.PopContext();
        ReadEnd(p, name, at);
        return rows;
    }

    private static bool IsEnd(Token t) => t.Kind == TokenKind.Command && t.Text == "\\end";

    private static void ReadEnd(LatexParser p, string name, int at)
    {
        var end = p.Peek();
        if (!IsEnd(end))
            throw p.Error($"missing \\end{{{name}}}", at);
        p.Next();
        var found = ReadName(p);
        if (found != name)
            throw p.Error($"\\begin{{{name}}} closed by \\end{{{found}}} (expected \\end{{{name}}})", end.Position);
    }

    /// <summary>
    /// Column alignment from an <c>array</c> specification: <c>l c r</c> are kept (trailing <c>c</c> trimmed), vertical
    /// rules (<c>| :</c>) are dropped with one warning, anything else is an error. All-centered gives null.
    /// </summary>
    private static string? ColumnAlign(LatexParser p, string spec, int at)
    {
        var columns = new System.Text.StringBuilder();
        bool rules = false;
        foreach (var c in spec)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (c is 'l' or 'c' or 'r') columns.Append(c);
            else if (c is '|' or ':') rules = true;
            else throw p.Error($"unsupported column specifier '{c}'", at);
        }
        if (rules)
            p.Warn("lossy", "vertical rules in the column specification are dropped", at);
        var align = columns.ToString().TrimEnd('c');
        return align.Length == 0 ? null : align;
    }
}
