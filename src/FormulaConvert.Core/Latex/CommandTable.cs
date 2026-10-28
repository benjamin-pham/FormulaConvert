using FormulaConvert.Ast;
using FormulaConvert.Latex.Commands;

namespace FormulaConvert.Latex;

/// <summary>Handles a prefix command; called after the command token has been consumed.</summary>
internal delegate FormulaNode CommandHandler(LatexParser p, string name);

/// <summary>
/// Handles an infix command (<c>\over</c>, <c>\atop</c>, …); called after the command token has been consumed,
/// with the items parsed so far in the current row. The result replaces the whole row.
/// </summary>
internal delegate FormulaNode InfixHandler(LatexParser p, string name, List<FormulaNode> left);

/// <summary>Command name (with backslash, e.g. <c>\frac</c>) to handler.</summary>
internal sealed class CommandTable
{
    private readonly Dictionary<string, CommandHandler> _prefix = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InfixHandler> _infix = new(StringComparer.Ordinal);

    /// <summary>The table used by <see cref="LatexParser.Parse"/>. Each command group registers itself here.</summary>
    public static CommandTable Default { get; } = CreateDefault();

    private static CommandTable CreateDefault()
    {
        var table = new CommandTable();
        SymbolCommands.Register(table);
        FractionCommands.Register(table);
        RadicalCommands.Register(table);
        NaryCommands.Register(table);
        FunctionCommands.Register(table);
        DelimiterCommands.Register(table);
        EnvironmentCommands.Register(table);
        AccentCommands.Register(table);
        OverUnderCommands.Register(table);
        // Later command groups: add one `XxxCommands.Register(table);` line each.
        return table;
    }

    public void Add(string name, CommandHandler h)
    {
        if (Has(name)) throw new InvalidOperationException($"Command {name} is already registered.");
        _prefix.Add(name, h);
    }

    public void AddInfix(string name, InfixHandler h)
    {
        if (Has(name)) throw new InvalidOperationException($"Command {name} is already registered.");
        _infix.Add(name, h);
    }

    public bool Has(string name) => _prefix.ContainsKey(name) || _infix.ContainsKey(name);

    public bool TryGet(string name, out CommandHandler handler) => _prefix.TryGetValue(name, out handler!);

    public bool TryGetInfix(string name, out InfixHandler handler) => _infix.TryGetValue(name, out handler!);
}
