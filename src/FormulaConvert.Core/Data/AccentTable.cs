namespace FormulaConvert.Data;

internal static class AccentTable
{
    private static readonly Lazy<(Dictionary<string, string> ByCommand, Dictionary<string, string> ByChar)> Data = new(() =>
    {
        var byCommand = new Dictionary<string, string>(StringComparer.Ordinal);
        var byChar = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in CsvResource.ReadRows("accents.csv"))
        {
            var chr = char.ConvertFromUtf32(CsvResource.ParseHex(r[1]));
            byCommand.TryAdd(r[0], chr);
            byChar.TryAdd(chr, r[0]);
        }
        return (byCommand, byChar);
    });

    /// <summary>All accent commands in <c>accents.csv</c>.</summary>
    public static IEnumerable<string> Commands => Data.Value.ByCommand.Keys;

    public static bool TryGetByCommand(string command, out string chr) =>
        Data.Value.ByCommand.TryGetValue(command, out chr!);

    public static bool TryGetByChar(string chr, out string command) =>
        Data.Value.ByChar.TryGetValue(chr, out command!);
}
