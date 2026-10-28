using FormulaConvert.Ast;

namespace FormulaConvert.Latex.Commands;

/// <summary><c>\boxed</c>, <c>\cancel</c> and friends, <c>\enclose</c>, and the phantoms <c>\phantom</c>, <c>\hphantom</c>, <c>\vphantom</c>, <c>\smash</c>.</summary>
internal static class BoxCommands
{
    private const BoxEdges All = BoxEdges.Top | BoxEdges.Bottom | BoxEdges.Left | BoxEdges.Right;

    /// <summary><c>\enclose</c> notations in canonical writing order (<c>box</c> stands for all four edges).</summary>
    internal static readonly (string Name, BoxEdges Edges)[] Notations =
    [
        ("box", All),
        ("top", BoxEdges.Top),
        ("bottom", BoxEdges.Bottom),
        ("left", BoxEdges.Left),
        ("right", BoxEdges.Right),
        ("horizontalstrike", BoxEdges.StrikeH),
        ("verticalstrike", BoxEdges.StrikeV),
        ("updiagonalstrike", BoxEdges.StrikeUp),
        ("downdiagonalstrike", BoxEdges.StrikeDown),
    ];

    public static void Register(CommandTable t)
    {
        t.Add("\\boxed", (p, n) => new BorderBox(All, p.ReadArgument()));
        t.Add("\\cancel", (p, n) => new BorderBox(BoxEdges.StrikeUp, p.ReadArgument()));
        t.Add("\\bcancel", (p, n) => new BorderBox(BoxEdges.StrikeDown, p.ReadArgument()));
        t.Add("\\xcancel", (p, n) => new BorderBox(BoxEdges.StrikeUp | BoxEdges.StrikeDown, p.ReadArgument()));
        t.Add("\\enclose", (p, n) => Enclose(p));
        t.Add("\\phantom", (p, n) => new Phantom(false, false, false, false, p.ReadArgument()));
        t.Add("\\hphantom", (p, n) => new Phantom(false, false, true, true, p.ReadArgument()));
        t.Add("\\vphantom", (p, n) => new Phantom(false, true, false, false, p.ReadArgument()));
        t.Add("\\smash", (p, n) =>
        {
            p.ReadRawOptional(); // [t] / [b] only changes which side is smashed; the canonical form smashes both
            return new Phantom(true, false, true, true, p.ReadArgument());
        });
    }

    private static FormulaNode Enclose(LatexParser p)
    {
        int at = p.Position;
        var notations = p.ReadRawGroup();
        var style = p.ReadRawOptional();
        var body = p.ReadArgument();
        var edges = BoxEdges.None;
        foreach (var word in notations.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word == "roundedbox")
            {
                p.Warn("lossy", "roundedbox is read as box (OMML has no rounded corners)", at);
                edges |= All;
            }
            else if (Array.FindIndex(Notations, x => x.Name == word) is >= 0 and var i)
            {
                edges |= Notations[i].Edges;
            }
            else
            {
                p.Warn("out-of-scope", $"enclose notation {word} has no OMML equivalent and is dropped", at);
            }
        }
        if (style != null)
            p.Warn("out-of-scope", $"enclose style [{style}] is dropped", at);
        return edges == BoxEdges.None ? body : new BorderBox(edges, body);
    }
}
