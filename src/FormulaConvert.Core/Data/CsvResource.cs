using System.Reflection;

namespace FormulaConvert.Data;

/// <summary>Reads embedded data files (CSV or one-item-per-line text).</summary>
internal static class CsvResource
{
    /// <summary>
    /// Returns the non-empty, non-comment (<c>#</c>) lines of an embedded resource,
    /// split on commas into at most <paramref name="maxFields"/> fields (the last field keeps any commas).
    /// </summary>
    public static IEnumerable<string[]> ReadRows(string resourceName, int maxFields = int.MaxValue)
    {
        var asm = typeof(CsvResource).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            rows.Add(line.Split(',', maxFields));
        }
        return rows;
    }

    public static int ParseHex(string s) => int.Parse(s, System.Globalization.NumberStyles.HexNumber);
}
