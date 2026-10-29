# FormulaConvert — OMML ↔ LaTeX (MathLive)

A two-way converter between **OMML** (Word equations, the ECMA-376 `m:` namespace) and **MathLive-flavored LaTeX** for scientific formulas (mathematics, physics, chemistry, etc.), written in C# (.NET 10).

Use case: users write equations in MathLive on the web; a C# backend writes them into Word (OMML) and reads them back to LaTeX for further editing.

## Quick start

```bash
# LaTeX -> OMML (reads stdin when no file is given)
echo '\frac{a}{b}+\sqrt{x}' | dotnet run --project src/FormulaConvert.Cli -- latex2omml

# OMML -> LaTeX
dotnet run --project src/FormulaConvert.Cli -- omml2latex formula.xml
```

- The result goes to stdout, warnings go to stderr (for example `warning [out-of-scope] position 0: \textcolor is not supported by OMML`).
- Exit code: `0` on success (warnings included), `1` on a syntax or input error.

## Library use

```csharp
using FormulaConvert.Core;

ConvertResult r = FormulaConverter.LatexToOmml(@"\frac{a}{b}");
string omml = r.Output;
foreach (Diagnostic d in r.Diagnostics) { /* code, message, position */ }

string latex = FormulaConverter.OmmlToLatex(omml).Output;
```

Syntax errors throw `FormulaParseException` with a position (`-1` for XML errors). The public surface is only `FormulaConverter`, `ConvertResult`, `Diagnostic` and `FormulaParseException`.

## Scope

Only the **intersection** of OMML and MathLive: fractions, radicals, scripts, n-ary operators, functions, delimiters, matrices and environments, accents, bars, boxes, phantoms, font styles and about 700 single symbols. All 732 commands in the [MathLive command reference](https://mathlive.io/mathfield/reference/commands/) are classified, and a test enforces it.

Out-of-scope commands (color, font size, `\href`, spacing, `\ce{}` and so on): the wrapper is dropped, the **content is kept**, and a `Diagnostic` is emitted (`out-of-scope`). Other codes: `lossy`, `unsupported-omml`.

## Known limits

- `\dfrac` and `\tfrac` are read but written back as `\frac` (with a warning); `\mathit` has no effect.
- Round trips are **equivalent after normalization**, not character-identical.
- The generated OMML follows ECMA-376 but has **not been tested against real Word**.
- No direct `.docx` reading or writing; input and output are OMML XML and LaTeX strings.
- Nesting limit: 200 levels of LaTeX; deeper input throws `FormulaParseException`.
- DTDs are prohibited when reading XML.

## Layout

```
src/FormulaConvert.Core/     library: Latex/ (parser, writer), Omml/ (reader, writer), Ast/, Data/ (embedded CSV tables)
src/FormulaConvert.Cli/      console app
tests/FormulaConvert.Tests/  xUnit tests
tools/extract_symbols.py  dev tool that builds the symbol tables from MathLive
docs/superpowers/         design spec and implementation plan (Vietnamese)
```

Architecture: `OMML → OmmlReader → AST → LatexWriter → LaTeX` and `LaTeX → LatexParser → AST → OmmlWriter → OMML`.

## Development

```bash
dotnet build
dotnet test
```

## Third-party data

The symbol tables come from MathLive (MIT). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
