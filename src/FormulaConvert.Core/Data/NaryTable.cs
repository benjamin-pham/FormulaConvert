namespace FormulaConvert.Data;

internal sealed record NaryInfo(string Command, string Chr, bool SubSupDefault);

internal static class NaryTable
{
    private static readonly Lazy<(Dictionary<string, NaryInfo> ByCommand, Dictionary<string, NaryInfo> ByChar)> Data = new(() =>
    {
        var byCommand = new Dictionary<string, NaryInfo>(StringComparer.Ordinal);
        var byChar = new Dictionary<string, NaryInfo>(StringComparer.Ordinal);
        foreach (var r in CsvResource.ReadRows("nary.csv"))
        {
            var info = new NaryInfo(r[0], char.ConvertFromUtf32(CsvResource.ParseHex(r[1])), bool.Parse(r[2]));
            byCommand.TryAdd(info.Command, info);
            byChar.TryAdd(info.Chr, info);
        }
        return (byCommand, byChar);
    });

    public static bool TryGetByCommand(string command, out NaryInfo info) =>
        Data.Value.ByCommand.TryGetValue(command, out info!);

    public static bool TryGetByChar(string chr, out NaryInfo info) =>
        Data.Value.ByChar.TryGetValue(chr, out info!);
}
