using System.Text;
using FormulaConvert.Ast;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    /// <summary><c>\left( a \middle| b \right)</c>; an absent delimiter is written <c>.</c>.</summary>
    private static void WriteDelimited(Delimited d, LatexBuilder b)
    {
        b.Command("\\left");
        WriteDelimiter(d.Open, b);
        for (int i = 0; i < d.Parts.Count; i++)
        {
            if (i > 0)
            {
                b.Command("\\middle");
                WriteDelimiter(d.Separator, b);
            }
            WriteNode(d.Parts[i], b);
        }
        b.Command("\\right");
        WriteDelimiter(d.Close, b);
    }

    private static void WriteDelimiter(string delimiter, LatexBuilder b)
    {
        switch (delimiter)
        {
            case "":
                b.Text(".");
                return;
            case "{":
                b.Command("\\lbrace");
                return;
            case "}":
                b.Command("\\rbrace");
                return;
            case "|" or "∣":
                b.Text("|");
                return;
            case "‖" or "∥":
                b.Command("\\Vert");
                return;
        }
        var rune = Rune.GetRuneAt(delimiter, 0);
        if (rune.Value >= 0x80 && Data.SymbolTable.TryGetCommand(rune.Value, out var command))
            b.Command(command);
        else
            b.Text(rune.ToString());
    }
}
