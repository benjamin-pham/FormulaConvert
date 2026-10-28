using FormulaConvert.Ast;
using FormulaConvert.Latex.Commands;

namespace FormulaConvert.Latex;

internal static partial class LatexWriter
{
    private const BoxEdges AllEdges = BoxEdges.Top | BoxEdges.Bottom | BoxEdges.Left | BoxEdges.Right;

    /// <summary>A border box as <c>\boxed</c>, <c>\cancel</c>, <c>\bcancel</c>, <c>\xcancel</c> or an <c>\enclose</c> notation list; no edges at all is just the body.</summary>
    private static void WriteBorderBox(BorderBox box, LatexBuilder bld)
    {
        string? shorthand = box.Edges switch
        {
            AllEdges => "\\boxed",
            BoxEdges.StrikeUp => "\\cancel",
            BoxEdges.StrikeDown => "\\bcancel",
            BoxEdges.StrikeUp | BoxEdges.StrikeDown => "\\xcancel",
            _ => null,
        };
        if (shorthand != null)
        {
            bld.Command(shorthand);
            WriteGroup(box.Body, bld);
            return;
        }
        if (box.Edges == BoxEdges.None)
        {
            WriteNode(box.Body, bld);
            return;
        }
        var words = new List<string>();
        var rest = box.Edges;
        if ((rest & AllEdges) == AllEdges)
        {
            words.Add("box");
            rest &= ~AllEdges;
        }
        foreach (var (name, edges) in BoxCommands.Notations)
            if (name != "box" && (rest & edges) != 0) words.Add(name);
        bld.Command("\\enclose").Text("{" + string.Join(' ', words) + "}");
        WriteGroup(box.Body, bld);
    }

    /// <summary>A phantom as its command; shapes without a command degrade to the nearest one (or to the bare body when shown).</summary>
    private static void WritePhantom(Phantom ph, LatexBuilder bld)
    {
        string? command = !ph.Show
            ? ph.ZeroAscent && ph.ZeroDescent && !ph.ZeroWidth ? "\\hphantom"
            : ph.ZeroWidth && !ph.ZeroAscent && !ph.ZeroDescent ? "\\vphantom"
            : "\\phantom"
            : ph.ZeroAscent && ph.ZeroDescent ? "\\smash" : null;
        if (command == null)
        {
            WriteNode(ph.Body, bld);
            return;
        }
        bld.Command(command);
        WriteGroup(ph.Body, bld);
    }
}
