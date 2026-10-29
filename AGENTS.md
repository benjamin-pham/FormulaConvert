# AGENTS.md

Guidance for agents working in this repo. For a project overview, see `README.md`.

## Commands

```bash
dotnet build
dotnet test          # must be green before committing
dotnet run --project src/FormulaConvert.Cli -- latex2omml|omml2latex [file]
```

## Source documents

- Spec: `docs/superpowers/specs/2026-10-28-omml-latex-converter-design.md` (Vietnamese)
- Plan: `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` (Vietnamese). Its "Quy ước" section defines the canonical LaTeX the writer must produce. If you change writer behavior, update the plan.

## Architecture

A shared internal AST serves both directions: `OmmlReader`/`LatexParser` → AST → `LatexWriter`/`OmmlWriter`. Each feature group lives in its own partial file (`Omml/`, `Latex/`, `Latex/Commands/`).

- Only `FormulaConverter`, `ConvertResult`, `Diagnostic` and `FormulaParseException` are `public`. AST types must stay `internal`; `PublicSurfaceTests` checks this.
- Symbol data lives in embedded CSV/TXT files in `src/FormulaConvert.Core/Data/`. Nothing is downloaded at run time.
- `tools/extract_symbols.py` builds the tables from MathLive. It only reads text; never execute downloaded code.

## Rules

- TDD: write the failing test first, then fix. Tests use the harness in `tests/FormulaConvert.Tests/Harness`; `CaseRunner` checks LaTeX→OMML, OMML→canonical LaTeX, and re-parsing the canonical form.
- When adding or changing a LaTeX command, update the data tables and `CatalogTests` (every command in `mathlive-commands.txt` must belong to exactly one category).
- OMML writer: keep it minimal (omit default properties) and follow the ECMA-376 element order. Never emit schema-invalid empty elements (`m:m`, `m:mr`, `m:eqArr`).
- A `m:bar` without `m:pos` means underline; the writer always writes `m:pos` explicitly.
- Out-of-scope commands: drop the wrapper, keep the content, emit a `Diagnostic` (`out-of-scope`, `lossy` or `unsupported-omml`).
- Untrusted input must never throw anything except `FormulaParseException`. Keep `NestingGuard` (nesting limit and stack check), the DTD ban, and the rejection of invalid XML code points.
- No Word is available for comparison; OMML changes should cite ECMA-376.

## Git

- Commit directly on `main`; there is no remote yet.
- Commit messages use `feat:`, `fix:`, `chore:` or `docs:` prefixes.
- Do not commit `bin/`, `obj/` or `.superpowers/` (all in `.gitignore`).
