namespace FormulaConvert;

public sealed record Diagnostic(string Code, string Message, int Position);

public sealed record ConvertResult(string Output, IReadOnlyList<Diagnostic> Diagnostics);

public sealed class FormulaParseException : Exception
{
    public FormulaParseException(string message, int position) : base(message)
    {
        Position = position;
    }

    public int Position { get; }
}
