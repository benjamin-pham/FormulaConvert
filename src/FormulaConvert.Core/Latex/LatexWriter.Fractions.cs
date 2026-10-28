using FormulaConvert.Ast;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    private static void WriteFrac(Frac f, LatexBuilder b)
    {
        switch (f.Kind)
        {
            case FracKind.NoBar:
                b.Text("{");
                WriteNode(f.Num, b);
                b.Text(" \\atop ");
                WriteNode(f.Den, b);
                b.Text("}");
                break;
            default:
                b.Command(f.Kind == FracKind.Lin ? "\\nicefrac" : "\\frac");
                WriteGroup(f.Num, b);
                WriteGroup(f.Den, b);
                break;
        }
    }

    /// <summary>A binomial (see <see cref="Delimited.IsBinomial"/>) as <c>\binom{n}{k}</c>.</summary>
    private static void WriteBinomial(FormulaNode top, FormulaNode bottom, LatexBuilder b)
    {
        b.Command("\\binom");
        WriteGroup(top, b);
        WriteGroup(bottom, b);
    }

    private static void WriteGroup(FormulaNode node, LatexBuilder b)
    {
        b.Text("{");
        WriteNode(node, b);
        b.Text("}");
    }
}
