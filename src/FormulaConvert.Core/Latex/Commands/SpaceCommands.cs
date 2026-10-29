using FormulaConvert.Ast;
using FormulaConvert.Data;

namespace FormulaConvert.Latex.Commands;

/// <summary>
/// Spacing commands: positive spacing commands emit Unicode spaces in OMML;
/// negative space <c>\!</c> is dropped with a <c>lossy</c> diagnostic.
/// </summary>
internal static class SpaceCommands
{
    private static readonly string[] Commands =
    [
        "\\,", "\\:", "\\>", "\\;",
        "\\quad", "\\qquad", "\\enspace", "\\enskip",
        "\\ ", "\\space", "\\nobreakspace",
    ];

    public static void Register(CommandTable t)
    {
        foreach (var cmd in Commands)
        {
            if (SpaceTable.TryGetText(cmd, out var text))
            {
                t.Add(cmd, (p, n) => LatexParser.CharRun(text));
            }
        }

        t.Add("\\!", (p, n) =>
        {
            int at = p.Peek(-1).Position;
            p.Warn("lossy", "\\! is not supported by OMML", at);
            return FormulaRow.Empty;
        });
    }
}
