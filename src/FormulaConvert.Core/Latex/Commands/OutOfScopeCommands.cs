using System.Text.RegularExpressions;
using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// Commands OMML cannot express (colors, sizes, spacing, html hooks, …): the wrapper is dropped, the kept argument
/// stays, and one <c>out-of-scope</c> diagnostic is added. Rules come from <c>outofscope.csv</c>
/// (<c>command,arity,kept_arg_index</c>); every argument except the kept one is read as raw text, not math.
/// </summary>
internal static partial class OutOfScopeCommands
{
    /// <summary>Commands that take a leading optional <c>[…]</c> argument.</summary>
    private static readonly HashSet<string> WithOptional = new(StringComparer.Ordinal) { "\\bbox", "\\rule", "\\raisebox", "\\stackbin" };

    /// <summary>CSV rows another group owns (the stray forms are reported as out-of-scope by NaryCommands).</summary>
    private static readonly HashSet<string> OwnedElsewhere = new(StringComparer.Ordinal) { "\\limits", "\\nolimits", "\\displaylimits" };

    private static readonly HashSet<string> BareDimensionSpacing = new(StringComparer.Ordinal)
    {
        "\\kern", "\\mkern", "\\mskip", "\\hskip", "\\mspace"
    };

    private static readonly HashSet<string> BareDimensionShift = new(StringComparer.Ordinal)
    {
        "\\lower", "\\raise"
    };

    [GeneratedRegex(@"^-?[0-9.]+\s*(?:em|ex|pt|mu|px|cm|mm|in)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DimensionRegex();

    public static void Register(CommandTable t)
    {
        foreach (var name in OutOfScopeTable.Names)
        {
            if (OwnedElsewhere.Contains(name)) continue;
            if (t.Has(name)) throw new InvalidOperationException($"outofscope.csv row {name} is already registered by another group.");
            OutOfScopeTable.TryGet(name, out var rule);
            if (BareDimensionSpacing.Contains(name))
                t.Add(name, (p, n) => HandleDimensionSpacing(p, n));
            else if (BareDimensionShift.Contains(name))
                t.Add(name, (p, n) => HandleDimensionShift(p, n));
            else
                t.Add(name, (p, n) => Handle(p, n, rule.Arity, rule.KeptArg));
        }
    }

    private static FormulaNode HandleDimensionSpacing(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position;
        ReadDimension(p);
        p.Warn("out-of-scope", $"{name} is not supported by OMML", at);
        return FormulaRow.Empty;
    }

    private static FormulaNode HandleDimensionShift(LatexParser p, string name)
    {
        int at = p.Peek(-1).Position;
        ReadDimension(p);
        p.Warn("out-of-scope", $"{name} is not supported by OMML", at);
        return p.ReadArgument();
    }

    private static void ReadDimension(LatexParser p)
    {
        if (p.Peek().Kind == TokenKind.BeginGroup)
        {
            p.ReadRawGroup();
            return;
        }

        int pos = p.Peek().Position;
        if (pos >= p.Source.Length) return;

        var remaining = p.Source[pos..];
        var match = DimensionRegex().Match(remaining);
        if (match.Success)
        {
            int endPos = pos + match.Length;
            while (!p.AtEnd && p.Peek().Position < endPos)
            {
                p.Next();
            }
            return;
        }

        if (p.HasArgument)
            p.Next();
    }

    private static FormulaNode Handle(LatexParser p, string name, int arity, int keptArg)
    {
        int at = p.Peek(-1).Position; // the command token, already consumed
        if (WithOptional.Contains(name)) p.ReadRawOptional();
        if (name == "\\hspace" && p.Peek() is { Kind: TokenKind.Char, Text: "*" }) p.Next();
        FormulaNode kept = FormulaRow.Empty;
        string? raw = null;
        for (int i = 0; i < arity; i++)
        {
            if (i == keptArg) kept = p.ReadArgument();
            else raw = ReadRaw(p);
        }
        bool suppressWarning = name is "\\mathrel" or "\\mathbin" && kept is Run r && r.Text.EnumerateRunes().Take(2).Count() == 1;
        if (!suppressWarning)
            p.Warn("out-of-scope", $"{name} is not supported by OMML", at);

        // \ce{H2O}: the chemical formula survives as normal text
        if (name is "\\ce" or "\\pu" && !string.IsNullOrEmpty(raw))
            return new Run(raw, new RunStyle(Normal: true));
        return kept;
    }

    /// <summary>A non-math argument: a raw <c>{…}</c> group or a single token.</summary>
    private static string ReadRaw(LatexParser p)
    {
        var t = p.Peek();
        if (t.Kind == TokenKind.BeginGroup) return p.ReadRawGroup();
        if (!p.HasArgument) throw p.Error("missing argument");
        return p.Next().Text;
    }
}
