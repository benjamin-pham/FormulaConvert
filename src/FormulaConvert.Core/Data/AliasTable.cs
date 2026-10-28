namespace FormulaConvert.Data;

internal static class AliasTable
{
    private static readonly Lazy<Dictionary<string, (int Arity, string Replacement)>> Data = new(() =>
    {
        var d = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        foreach (var r in CsvResource.ReadRows("aliases.csv", 3))
            d.TryAdd(r[0], (int.Parse(r[1]), r[2]));
        return d;
    });

    public static bool TryGet(string command, out (int Arity, string Replacement) alias) =>
        Data.Value.TryGetValue(command, out alias);
}
