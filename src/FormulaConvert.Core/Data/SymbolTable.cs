namespace FormulaConvert.Data;

/// <summary>Command to Unicode codepoint table (symbols.csv); the first row per codepoint is canonical.</summary>
internal static class SymbolTable
{
    private static readonly Lazy<(Dictionary<string, int> ByCommand, Dictionary<int, string> ByCodepoint)> Data = new(Load);

    private static (Dictionary<string, int>, Dictionary<int, string>) Load()
    {
        var byCommand = new Dictionary<string, int>(StringComparer.Ordinal);
        var byCodepoint = new Dictionary<int, string>();
        foreach (var r in CsvResource.ReadRows("symbols.csv"))
        {
            var cp = CsvResource.ParseHex(r[1]);
            byCommand.TryAdd(r[0], cp);
            byCodepoint.TryAdd(cp, r[0]);
        }
        return (byCommand, byCodepoint);
    }

    public static bool TryGetCodepoint(string command, out int codepoint) =>
        Data.Value.ByCommand.TryGetValue(command, out codepoint);

    public static bool TryGetCommand(int codepoint, out string command) =>
        Data.Value.ByCodepoint.TryGetValue(codepoint, out command!);
}
