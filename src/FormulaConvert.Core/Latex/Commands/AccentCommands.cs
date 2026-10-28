using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>Accents (<c>\hat</c>, <c>\vec</c>, …), bars and group characters (<c>\overline</c>, <c>\underbrace</c>, …); each takes one argument.</summary>
internal static class AccentCommands
{
    /// <summary>Accent commands that are not in <c>accents.csv</c> (the CSV's canonical name for the character wins when writing).</summary>
    internal static readonly (string Command, string Chr)[] ExtraAccents =
    [
        ("\\widecheck", "̌"),
        ("\\overrightarrow", "⃗"),
        ("\\overleftarrow", "⃖"),
        ("\\overleftrightarrow", "⃡"),
    ];

    /// <summary>Group-character commands: command, character, and whether it sits above the body (the first command per shape is canonical).</summary>
    internal static readonly (string Command, string Chr, bool Top)[] GroupChars =
    [
        ("\\overbrace", "⏞", true),
        ("\\underbrace", "⏟", false),
        ("\\overparen", "⏜", true),
        ("\\wideparen", "⏜", true),
        ("\\underparen", "⏝", false),
        ("\\underrightarrow", "→", false),
        ("\\underleftarrow", "←", false),
        ("\\underleftrightarrow", "↔", false),
    ];

    public static void Register(CommandTable t)
    {
        foreach (var command in AccentTable.Commands)
        {
            AccentTable.TryGetByCommand(command, out var chr);
            t.Add(command, (p, n) => new Accent(chr, p.ReadArgument()));
        }
        foreach (var (command, chr) in ExtraAccents)
            t.Add(command, (p, n) => new Accent(chr, p.ReadArgument()));
        t.Add("\\overline", (p, n) => new Bar(true, p.ReadArgument()));
        t.Add("\\underline", (p, n) => new Bar(false, p.ReadArgument()));
        foreach (var (command, chr, top) in GroupChars)
            t.Add(command, (p, n) => new GroupChar(chr, top, p.ReadArgument()));
    }
}
