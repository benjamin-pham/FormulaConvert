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

    /// <summary>A binomial (default-parenthesis <see cref="Delimited"/> around one bar-less fraction) as <c>\binom{n}{k}</c>.</summary>
    private static void WriteBinomial(Delimited d, LatexBuilder b)
    {
        var f = (Frac)d.Parts[0];
        b.Command("\\binom");
        WriteGroup(f.Num, b);
        WriteGroup(f.Den, b);
    }

    private static bool IsBinomial(Delimited d) =>
        d is { Open: "(", Close: ")", Parts: [Frac { Kind: FracKind.NoBar }] };

    private static void WriteGroup(FormulaNode node, LatexBuilder b)
    {
        b.Text("{");
        WriteNode(node, b);
        b.Text("}");
    }
}
