namespace FormulaConvert.Data;

internal static class FunctionTable
{
    private static readonly Lazy<HashSet<string>> Names = new(() =>
        new HashSet<string>(CsvResource.ReadRows("functions.txt").Select(r => r[0]), StringComparer.Ordinal));

    /// <summary>Functions whose sub/superscripts are placed below/above (limit-style).</summary>
    public static readonly IReadOnlySet<string> BoundsFunctions = new HashSet<string>(StringComparer.Ordinal)
    {
        "\\lim", "\\liminf", "\\limsup", "\\max", "\\min", "\\sup", "\\inf",
        "\\injlim", "\\projlim", "\\varliminf", "\\varlimsup", "\\varinjlim", "\\varprojlim",
    };

    /// <summary>All function commands (with backslash).</summary>
    public static IEnumerable<string> All => Names.Value;

    public static bool Contains(string command) => Names.Value.Contains(command);
}
