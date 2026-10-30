using System.Text.RegularExpressions;
using FormulaConvert.Tests.Harness;

namespace FormulaConvert.Tests;

public class WordGalleryTests
{
    [Theory]
    // 1. Built-in Equations
    [InlineData(@"A=\pi r^2")]
    [InlineData(@"(x+a)^n=\sum_{k=0}^{n}\binom{n}{k}x^k a^{n-k}")]
    [InlineData(@"(1+x)^n=1+\frac{nx}{1!}+\frac{n(n-1)x^2}{2!}+\cdots")]
    [InlineData(@"f(x)=a_0+\sum_{n=1}^{\infty}\left(a_n\cos\frac{n\pi x}{L}+b_n\sin\frac{n\pi x}{L}\right)")]
    [InlineData(@"a^2+b^2=c^2")]
    [InlineData(@"x=\frac{-b\pm\sqrt{b^2-4ac}}{2a}")]
    [InlineData(@"e^x=1+\frac{x}{1!}+\frac{x^2}{2!}+\frac{x^3}{3!}+\cdots,\quad -\infty<x<\infty")]
    [InlineData(@"\sin\alpha\pm\sin\beta=2\sin\frac{1}{2}(\alpha\pm\beta)\cos\frac{1}{2}(\alpha\mp\beta)")]
    [InlineData(@"\cos\alpha+\cos\beta=2\cos\frac{1}{2}(\alpha+\beta)\cos\frac{1}{2}(\alpha-\beta)")]

    // 2. Fraction
    [InlineData(@"\frac{dy}{dx}")]
    [InlineData(@"\nicefrac{a}{b}")]
    [InlineData(@"\frac{\partial y}{\partial x}")]
    [InlineData(@"\frac{\pi}{2}")]

    // 3. Script
    [InlineData(@"x_{y^2}")]
    [InlineData(@"e^{-i\omega t}")]
    [InlineData(@"{}_1^n Y")]

    // 4. Radical
    [InlineData(@"\sqrt[3]{x}")]
    [InlineData(@"\sqrt{a^2+b^2}")]

    // 5. Integral
    [InlineData(@"\int_0^1 x\,dx")]
    [InlineData(@"\int\limits_0^1 x\,dx")]
    [InlineData(@"\iiint_V f")]
    [InlineData(@"\oint_C F")]
    [InlineData(@"\oiint_S F")]
    [InlineData(@"\oiiint_V F")]

    // 6. Large Operator
    [InlineData(@"\sum\limits_{i=1}^n i")]
    [InlineData(@"\prod_i x_i")]
    [InlineData(@"\bigcup_{i=1}^n A_i")]
    [InlineData(@"\bigwedge_i p_i")]

    // 7. Bracket
    [InlineData(@"\left\lfloor x\right\rfloor")]
    [InlineData(@"\left\lceil x\right\rceil")]
    [InlineData(@"\left[a,b\right[")]
    [InlineData(@"\left\llbracket x\right\rrbracket")]
    [InlineData(@"\left\langle a\middle|b\middle|c\right\rangle")]
    [InlineData(@"\left.x\right|")]
    [InlineData(@"\begin{cases}-x&x<0\\x&x\ge 0\end{cases}")]
    [InlineData(@"\left\langle{n \atop k}\right\rangle")]

    // 8. Function
    [InlineData(@"\sin^{-1}\theta")]
    [InlineData(@"\sech x")]
    [InlineData(@"\tan\theta=\frac{\sin\theta}{\cos\theta}")]

    // 9. Accent
    [InlineData(@"\dddot{x}")]
    [InlineData(@"\overline{\overline{x}}")]
    [InlineData(@"\overbrace{a+b}^{n}")]
    [InlineData(@"\underbrace{x+y}_{m}")]
    [InlineData(@"\overleftrightarrow{AB}")]
    [InlineData(@"\overrightharpoon{x}")]
    [InlineData(@"\boxed{a^2=b^2+c^2}")]
    [InlineData(@"\overline{x\oplus y}")]

    // 10. Limit and Log
    [InlineData(@"\log_b x")]
    [InlineData(@"\lim_{n\to\infty}\left(1+\frac{1}{n}\right)^n")]
    [InlineData(@"\max_{0\le x\le 1}xe^{-x^2}")]

    // 11. Operator
    [InlineData(@"\overset{\text{def}}{=}")]
    [InlineData(@"\measeq")]
    [InlineData(@"\triangleq")]
    [InlineData(@"\xrightarrow{yields}")]
    [InlineData(@"\xrightarrow{\Delta}")]
    [InlineData(@"\xLeftarrow[b]{a}")]

    // 12. Matrix
    [InlineData(@"\begin{pmatrix}1&\cdots&1\\\vdots&\ddots&\vdots\end{pmatrix}")]
    [InlineData(@"\begin{Vmatrix}a&b\\c&d\end{Vmatrix}")]
    [InlineData(@"\begin{matrix}1&\\&1\end{matrix}")]

    // 13. Symbols
    [InlineData(@"a\nleq b\approx c\parallel d")]
    [InlineData(@"\mathbb{R}\mathscr{L}\mathfrak{g}")]
    [InlineData(@"a\mathrel{≢}b")]
    [InlineData(@"\int f\,dx")]
    public void Word_gallery_formula_round_trips(string latex)
    {
        // 1. LatexToOmml does not throw and has no out-of-scope diagnostics
        var toOmml = FormulaConverter.LatexToOmml(latex);
        Assert.DoesNotContain(toOmml.Diagnostics, d => d.Code == "out-of-scope");

        // 2. OmmlToLatex output commands all belong to MathLive defined catalog
        var toLatex = FormulaConverter.OmmlToLatex(toOmml.Output);
        foreach (Match m in Regex.Matches(toLatex.Output, @"\\(?:begin|end)\{([^}]+)\}"))
        {
            var env = m.Groups[1].Value;
            Assert.True(MathLiveCatalog.Environments.Contains(env),
                $"Environment '{env}' in `{toLatex.Output}` from `{latex}` is not defined in MathLive");
        }
        foreach (Match m in Regex.Matches(toLatex.Output, @"\\(?:[A-Za-z]+|[^A-Za-z\s]| )"))
        {
            var cmd = m.Value;
            if (cmd is @"\begin" or @"\end" or @"\\") continue;
            Assert.True(MathLiveCatalog.Defined.Contains(cmd),
                $"Command '{cmd}' in `{toLatex.Output}` from `{latex}` is not defined in MathLive");
        }

        // 3. Re-parsing the canonical LaTeX produces the exact same OMML (round-trip)
        var roundTripOmml = FormulaConverter.LatexToOmml(toLatex.Output);
        Assert.True(Xml.SameXml(toOmml.Output, roundTripOmml.Output),
            $"Round trip OMML mismatch for `{latex}`\nCanonical LaTeX: `{toLatex.Output}`\nInitial OMML:    {toOmml.Output}\nRound-trip OMML: {roundTripOmml.Output}");
    }
}
