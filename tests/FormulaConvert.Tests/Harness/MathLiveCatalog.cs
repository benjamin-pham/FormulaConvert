namespace FormulaConvert.Tests.Harness;

public static class MathLiveCatalog
{
    private static readonly Lazy<(HashSet<string> Defined, HashSet<string> Environments, HashSet<string> All)> Data = new(Load);

    private static (HashSet<string>, HashSet<string>, HashSet<string>) Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "mathlive-defined.txt");
        if (!File.Exists(path))
        {
            path = Path.Combine("tests", "FormulaConvert.Tests", "Data", "mathlive-defined.txt");
        }
        if (!File.Exists(path))
            throw new FileNotFoundException($"Cannot find mathlive-defined.txt at {path}");

        var defined = new HashSet<string>(StringComparer.Ordinal);
        var environments = new HashSet<string>(StringComparer.Ordinal);
        var all = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrEmpty(line)) continue;
            all.Add(line);
            if (line.StartsWith("env:", StringComparison.Ordinal))
            {
                var envName = line["env:".Length..];
                environments.Add(envName);
                environments.Add(line);
            }
            else if (line.StartsWith('\\'))
            {
                defined.Add(line);
            }
        }
        return (defined, environments, all);
    }

    public static IReadOnlySet<string> Defined => Data.Value.Defined;
    public static IReadOnlySet<string> Environments => Data.Value.Environments;
    public static IReadOnlySet<string> All => Data.Value.All;
}
