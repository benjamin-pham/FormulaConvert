using FormulaConvert.Data;

namespace FormulaConvert.Latex;

/// <summary>Tells which catalog tables know a command; used by the coverage checks.</summary>
internal static class CommandClassifier
{
    /// <summary>The groups (Symbol, Function, Nary, Accent, Space, Alias, OutOfScope, Structural) that contain the command.</summary>
    public static IReadOnlyList<string> Categories(string command)
    {
        var result = new List<string>();
        if (SymbolTable.TryGetCodepoint(command, out _)) result.Add("Symbol");
        if (FunctionTable.Contains(command)) result.Add("Function");
        if (NaryTable.TryGetByCommand(command, out _)) result.Add("Nary");
        if (AccentTable.TryGetByCommand(command, out _)) result.Add("Accent");
        if (SpaceTable.Has(command)) result.Add("Space");
        if (AliasTable.TryGet(command, out _)) result.Add("Alias");
        if (OutOfScopeTable.TryGet(command, out _)) result.Add("OutOfScope");
        if (CommandTable.Default.Has(command)) result.Add("Structural");
        return result;
    }
}
