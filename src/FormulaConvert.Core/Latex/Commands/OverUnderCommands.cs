using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary><c>\overset</c>, <c>\underset</c>, <c>\overunderset</c>, <c>\stackrel</c> and the extensible arrows <c>\xrightarrow[below]{above}</c>.</summary>
internal static class OverUnderCommands
{
    /// <summary>Extensible-arrow commands, canonical command first per character. The character is that of the command without its leading <c>x</c>, except where overridden.</summary>
    private static readonly string[] ArrowCommands =
    [
        "\\xrightarrow", "\\xleftarrow", "\\xRightarrow", "\\xLeftarrow", "\\xleftrightarrow", "\\xLeftrightarrow",
        "\\xhookleftarrow", "\\xhookrightarrow", "\\xtwoheadleftarrow", "\\xtwoheadrightarrow", "\\xlongequal",
        "\\xmapsto", "\\xtofrom", "\\xrightharpoonup", "\\xrightharpoondown", "\\xleftharpoonup", "\\xleftharpoondown",
        "\\xrightleftharpoons", "\\xleftrightharpoons",
    ];

    private static readonly Lazy<(Dictionary<string, string> ByCommand, Dictionary<string, string> ByChar)> Arrows = new(() =>
    {
        var byCommand = new Dictionary<string, string>(StringComparer.Ordinal);
        var byChar = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var command in ArrowCommands)
        {
            string? chr = command switch
            {
                "\\xlongequal" => "=",
                "\\xtofrom" => char.ConvertFromUtf32(SymbolTable.TryGetCodepoint("\\rightleftarrows", out var rl) ? rl : 0x21C4),
                "\\xmapsto" => char.ConvertFromUtf32(SymbolTable.TryGetCodepoint("\\mapsto", out var m) ? m : 0x21A6),
                _ => SymbolTable.TryGetCodepoint("\\" + command[2..], out var cp) ? char.ConvertFromUtf32(cp) : null,
            };
            if (chr == null) continue;
            byCommand.Add(command, chr);
            byChar.TryAdd(chr, command);
        }
        return (byCommand, byChar);
    });

    /// <summary>The extensible-arrow command whose arrow is <paramref name="chr"/>.</summary>
    internal static bool TryGetArrowCommand(string chr, out string command) =>
        Arrows.Value.ByChar.TryGetValue(chr, out command!);

    public static void Register(CommandTable t)
    {
        CommandHandler over = (p, n) =>
        {
            var lim = p.ReadArgument();
            return new Limit(p.ReadArgument(), lim, true);
        };
        t.Add("\\overset", over);
        t.Add("\\stackrel", over);
        t.Add("\\underset", (p, n) =>
        {
            var lim = p.ReadArgument();
            return new Limit(p.ReadArgument(), lim, false);
        });
        t.Add("\\overunderset", (p, n) =>
        {
            var above = p.ReadArgument();
            var below = p.ReadArgument();
            return new Limit(new Limit(p.ReadArgument(), below, false), above, true);
        });
        foreach (var (command, chr) in Arrows.Value.ByCommand)
        {
            t.Add(command, (p, n) =>
            {
                FormulaNode result = LatexParser.CharRun(chr);
                var below = p.ReadOptionalArgument();
                var above = p.ReadArgument();
                if (below != null) result = new Limit(result, below, false);
                if (!IsEmpty(above)) result = new Limit(result, above, true);
                return result;
            });
        }
    }

    private static bool IsEmpty(FormulaNode node) => node is FormulaRow { Items.Count: 0 };
}
