using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary>Radicals: <c>\sqrt[n]{x}</c>.</summary>
internal static class RadicalCommands
{
    public static void Register(CommandTable t)
    {
        t.Add("\\sqrt", (p, name) =>
        {
            var degree = p.ReadOptionalArgument();
            return new Radical(degree, p.ReadArgument());
        });
    }
}
