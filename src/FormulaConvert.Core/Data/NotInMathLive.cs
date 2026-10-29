using System.Diagnostics.CodeAnalysis;

namespace FormulaConvert.Data;

/// <summary>
/// Commands that MathLive does not define. These commands may still be parsed, but LatexWriter must not emit them
/// (writing \mathrel{c}, \mathbin{c}, bare characters, or \operatorname{...} instead).
/// </summary>
internal static class NotInMathLive
{
    private static readonly Lazy<Dictionary<string, string>> Data = new(Load);

    private static Dictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in CsvResource.ReadRows("not-in-mathlive.txt"))
        {
            map[r[0]] = r[1];
        }
        return map;
    }

    public static bool TryGetKind(string command, [NotNullWhen(true)] out string? kind)
    {
        if (Data.Value.TryGetValue(command, out kind)) return true;
        if (!command.StartsWith('\\') && Data.Value.TryGetValue("\\" + command, out kind)) return true;
        kind = null;
        return false;
    }
}
