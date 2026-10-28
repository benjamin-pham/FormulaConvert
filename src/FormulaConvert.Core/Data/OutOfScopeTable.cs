namespace FormulaConvert.Data;

internal static class OutOfScopeTable
{
    private static readonly Lazy<Dictionary<string, (int Arity, int KeptArg)>> Data = new(() =>
    {
        var d = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var r in CsvResource.ReadRows("outofscope.csv"))
            d.TryAdd(r[0], (int.Parse(r[1]), int.Parse(r[2])));
        return d;
    });

    public static IEnumerable<string> Names => Data.Value.Keys;

    public static bool TryGet(string command, out (int Arity, int KeptArg) rule) =>
        Data.Value.TryGetValue(command, out rule);
}
