using System.Text;

namespace FormulaConvert.Data;

/// <summary>
/// Bidirectional mapping between LaTeX spacing commands and Unicode space characters in OMML.
/// </summary>
internal static class SpaceTable
{
    private static readonly Dictionary<string, string> CommandToText = new(StringComparer.Ordinal)
    {
        ["\\,"] = "\u2009",
        ["\\:"] = "\u2005",
        ["\\>"] = "\u2005",
        ["\\;"] = "\u2004",
        ["\\quad"] = "\u2003",
        ["\\qquad"] = "\u2003\u2003",
        ["\\enspace"] = "\u2002",
        ["\\enskip"] = "\u2002",
        ["\\ "] = "\u00A0",
        ["~"] = "\u00A0",
        ["\\space"] = "\u00A0",
        ["\\nobreakspace"] = "\u00A0",
    };

    private static readonly Dictionary<int, string> CodepointToCommand = new()
    {
        [0x2009] = "\\,",
        [0x2006] = "\\,",
        [0x200A] = "\\,",
        [0x2005] = "\\:",
        [0x205F] = "\\:",
        [0x2004] = "\\;",
        [0x2003] = "\\quad",
        [0x2002] = "\\enspace",
        [0x00A0] = "\\ ",
        [0x0020] = "\\ ",
    };

    public static bool TryGetText(string command, out string text) =>
        CommandToText.TryGetValue(command, out text!);

    public static bool TryGetCommand(int cp, out string command) =>
        CodepointToCommand.TryGetValue(cp, out command!);

    public static bool IsSpace(int cp) =>
        CodepointToCommand.ContainsKey(cp) || (Rune.IsValid(cp) && Rune.IsWhiteSpace(new Rune(cp)));

    public static bool Has(string command) =>
        CommandToText.ContainsKey(command) || command == "\\!";
}
