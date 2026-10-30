using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary><c>\placeholder[id]{content}</c>: an empty input slot.</summary>
internal static class PlaceholderCommands
{
    public static void Register(CommandTable t)
    {
        t.Add("\\placeholder", (p, n) =>
        {
            int at = p.Peek(-1).Position;
            var id = p.ReadRawOptional();
            var body = p.ReadArgument();
            bool hasId = !string.IsNullOrWhiteSpace(id);
            bool hasContent = body is not FormulaRow { Items.Count: 0 };
            if (hasId || hasContent)
                p.Warn("lossy", "\\placeholder id or default content is not supported by OMML", at);
            return hasContent ? body : new Placeholder();
        });
    }
}
