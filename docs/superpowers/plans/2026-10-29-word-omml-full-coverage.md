# Word OMML 100% Formula Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Achieve 100% two-way coverage of Microsoft Word's Equation Editor structures and symbol galleries (`m:box`, `m:argPr`, `m:groupChr` extensible arrows and labeled braces, missing functions/operators/accents/symbols, PUA cleanup, and Unicode Mathematical Alphanumeric decoding).

**Architecture:** Extend the existing `FormulaNode` AST (`GroupChar.VertJcTop`) and the vertical slices in `OmmlReader`, `OmmlWriter`, `LatexParser`, and `LatexWriter`. Update the embedded CSV/TXT symbol tables in `src/FormulaConvert.Core/Data/` and add `MathAlphaDecoder` to normalize Unicode styled letters (`U+1D400..U+1D7FF` and BMP letterlike symbols) into styled ASCII `Run` nodes.

**Tech Stack:** C#, .NET 10, `System.Xml.Linq`, xUnit, embedded CSV/TXT resources.

**Spec:** `docs/superpowers/specs/2026-10-29-word-omml-full-coverage-design.md`

## Global Constraints

- Only `FormulaConverter`, `ConvertResult`, `Diagnostic` and `FormulaParseException` are `public`. AST types must stay `internal`; `PublicSurfaceTests` checks this.
- TDD: write the failing test first, then fix. Tests use the harness in `tests/FormulaConvert.Tests/Harness`; `CaseRunner` checks LaTeX→OMML, OMML→canonical LaTeX, and re-parsing the canonical form.
- When adding or changing a LaTeX command, update the data tables and `CatalogTests` (every command in `mathlive-commands.txt` must belong to at least one category).
- OMML writer: keep it minimal (omit default properties) and follow the ECMA-376 element order. Never emit schema-invalid empty elements (`m:m`, `m:mr`, `m:eqArr`).
- Untrusted input must never throw anything except `FormulaParseException`. Keep `NestingGuard` (nesting limit and stack check), the DTD ban, and the rejection of invalid XML code points.
- Update `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` whenever canonical writer conventions change.

## Review Focus

1. `<m:box>` without an `<m:e>` child (e.g., `<m:box><m:boxPr/></m:box>`) must not throw `NullReferenceException`; it produces an empty row without diagnostics (Task 1).
2. A script attached to an already-labeled `\overbrace{x}^y` or `\underbrace{x}_y` (e.g., `{\overbrace{x}^y}_z` or `{\overbrace{x}^y}^2`) must brace the inner `\overbrace{x}^y` when writing LaTeX so re-parsing does not fail with `double superscript` or attach `_z` directly to `\overbrace` (Task 2).
3. An extensible arrow whose optional below-label contains an unbraced `]` (e.g., `\xrightarrow[{a]b}]{c}`) must wrap the optional argument in `{...}` when written by `LatexWriter` so re-parsing does not close `[...]` early (Task 2).
4. Unicode Mathematical Alphanumeric characters (`ℝ`, `𝒜`, `𝐀`) and Unicode minus (`−` `U+2212`) inside `\text{...}` (`<m:nor/>`) must stay literal characters and never turn into `\mathbb{R}`, `\mathbf{A}`, or `-` (Task 4).
5. An `<m:r>` with an explicit `<m:rPr><m:sty m:val="b"/></m:rPr>` containing a BMP double-struck character `<m:t>ℝ</m:t>` must combine the explicit `m:sty` with the decoded `Scr.DoubleStruck` to produce `\mathbf{\mathbb{R}}` (Task 4).

---

### Task 1: OMML `m:box` and `m:argPr` support

**Files:**
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.cs:67-131`
- Test: `tests/FormulaConvert.Tests/BoxTests.cs`
- Test: `tests/FormulaConvert.Tests/FractionTests.cs`

**Interfaces:**
- Consumes: `OmmlReader.ReadInto(XElement container, List<FormulaNode> items, List<Diagnostic> d)`
- Produces: transparent handling of `m:argPr` (ignored) and `m:box` (reads child `m:e` if present, ignores `m:boxPr`) in `OmmlReader.ReadInto`.

- [ ] **Step 1: Write the failing tests in `tests/FormulaConvert.Tests/BoxTests.cs` and `tests/FormulaConvert.Tests/FractionTests.cs`**

In `tests/FormulaConvert.Tests/BoxTests.cs`, add:
```csharp
[Theory]
[InlineData("<m:box><m:boxPr><m:diff m:val=\"1\"/></m:boxPr><m:e><m:r><m:t>dx</m:t></m:r></m:e></m:box>", "dx")]
[InlineData("<m:box><m:boxPr><m:opEmu m:val=\"1\"/></m:boxPr><m:e><m:r><m:t>:=</m:t></m:r></m:e></m:box>", ":=")]
[InlineData("<m:box><m:boxPr/></m:box>", "")]
public void Reader_logical_box_reads_body_without_diagnostics(string inner, string expected)
{
    var r = FormulaConverter.OmmlToLatex(Wrap(inner));
    Assert.Equal(expected, r.Output);
    Assert.Empty(r.Diagnostics);
}
```

In `tests/FormulaConvert.Tests/FractionTests.cs`, add:
```csharp
[Fact]
public void Reader_small_fraction_ignores_argPr()
{
    var xml = "<m:f><m:fPr><m:type m:val=\"bar\"/></m:fPr>"
        + "<m:num><m:argPr><m:argSz m:val=\"-2\"/></m:argPr>" + R("a") + "</m:num>"
        + "<m:den><m:argPr><m:argSz m:val=\"-2\"/></m:argPr>" + R("b") + "</m:den></m:f>";
    var r = FormulaConverter.OmmlToLatex(OMath(xml));
    Assert.Equal("\\frac{a}{b}", r.Output);
    Assert.Empty(r.Diagnostics);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Reader_logical_box_reads_body_without_diagnostics|FullyQualifiedName~Reader_small_fraction_ignores_argPr"`
Expected: FAIL (`Assert.Empty()` fails with `unsupported-omml` diagnostics for `m:box`, `m:boxPr`, `m:e`, `m:argPr`, `m:argSz`).

- [ ] **Step 3: Implement `m:argPr` and `m:box` in `src/FormulaConvert.Core/Omml/OmmlReader.cs`**

In `OmmlReader.ReadInto`:
- Expand `case "ctrlPr":` to `case "ctrlPr" or "argPr": break;`
- Add `case "box": if (el.Element(M + "e") is { } boxBody) ReadInto(boxBody, items, d); break;`

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/FormulaConvert.Core/Omml/OmmlReader.cs tests/FormulaConvert.Tests/BoxTests.cs tests/FormulaConvert.Tests/FractionTests.cs
git commit -m "feat: support OMML m:box and m:argPr in OmmlReader"
```

---

### Task 2: Extensible arrows (`\xrightarrow`), labeled braces (`\overbrace{x}^y`), and double overbar (`U+033F`)

**Files:**
- Modify: `src/FormulaConvert.Core/Ast/FormulaNode.cs:131`
- Modify: `src/FormulaConvert.Core/Latex/ScriptParsing.cs:55-58`
- Modify: `src/FormulaConvert.Core/Latex/Commands/OverUnderCommands.cs:62-74`
- Modify: `src/FormulaConvert.Core/Omml/OmmlWriter.Accents.cs:17-22`
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.Accents.cs:8-29`
- Modify: `src/FormulaConvert.Core/Latex/LatexWriter.Accents.cs:29-90`
- Modify: `src/FormulaConvert.Core/Latex/LatexWriter.Scripts.cs:51-60`
- Modify: `docs/superpowers/plans/2026-10-28-omml-latex-converter.md`
- Test: `tests/FormulaConvert.Tests/AccentTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record GroupChar(string Chr, bool Top, FormulaNode Body, bool VertJcTop = false) : FormulaNode;`
  - `OmmlWriter.WriteGroupChar(GroupChar g)`: when `g.VertJcTop` is `true`, appends `<m:vertJc m:val="{(g.Top ? "bot" : "top")}"/>` after `<m:pos>` in `<m:groupChrPr>`.
  - `OmmlReader.ReadGroupChar(XElement el, List<Diagnostic> d)`: sets `VertJcTop = true` when `(pos != "top" && vertJc == "top")` or `(pos == "top" && vertJc == "bot" && OverUnderCommands.TryGetArrowCommand(chr, out _) && chr != "=")`.
  - `OmmlReader.ReadAccent(XElement el, List<Diagnostic> d)`: when `chr == "\u033F"`, returns `new Bar(true, new Bar(true, ReadContent(el.Element(M + "e"), d)))`.
  - `ScriptParsing.Attach`: when `atom` is `GroupChar { Chr: "⏞", Top: true, VertJcTop: false }` and `sup != null && sub == null`, returns `new Limit(atom, sup, true)`; when `atom` is `GroupChar { Chr: "⏟", Top: false, VertJcTop: false }` and `sub != null && sup == null`, returns `new Limit(atom, sub, false)`.
  - `OverUnderCommands.Register`: for every arrow command in `Arrows` with `chr != "="`, builds `GroupChar(chr, Top: false, above, VertJcTop: true)` when only `above` is non-empty, `GroupChar(chr, Top: true, below, VertJcTop: true)` when `above` is empty and `below != null`, `Limit(new GroupChar(chr, false, above, true), below, false)` when both are non-empty, and `Limit(LatexParser.CharRun(chr), FormulaRow.Empty, true)` when both are empty (`\xrightarrow{}`).

- [ ] **Step 1: Write the failing tests in `tests/FormulaConvert.Tests/AccentTests.cs`**

In `tests/FormulaConvert.Tests/AccentTests.cs`:
- Add helper:
```csharp
private static string GcV(string chr, bool top, string vertJc, string e) =>
    $"<m:groupChr><m:groupChrPr><m:chr m:val=\"{chr}\"/><m:pos m:val=\"{(top ? "top" : "bot")}\"/><m:vertJc m:val=\"{vertJc}\"/></m:groupChrPr><m:e>{e}</m:e></m:groupChr>";
```
- Update `Xrightarrow`, `Xrightarrow_below_and_above`, `Xleftarrow_empty_above_is_dropped`, `XRightarrow`, `Xmapsto`, `Xtofrom`, `Xleftrightarrow`, `Xhookrightarrow`, `Xrightarrow_with_complex_labels` to expect `GcV`/`LL(GcV(...), ...)` as the OMML output:
  - `Xrightarrow`: `Check("\\xrightarrow{a}", GcV("→", false, "top", R("a")));`
  - `Xrightarrow_below_and_above`: `Check("\\xrightarrow[b]{a}", LL(GcV("→", false, "top", R("a")), R("b")));`
  - `Xleftarrow_empty_above_is_dropped`: `Check("\\xleftarrow[b]{}", GcV("←", true, "bot", R("b")));`
  - `XRightarrow`: `Check("\\xRightarrow{a}", GcV("⇒", false, "top", R("a")));`
  - `Xmapsto`: `Check("\\xmapsto{a}", GcV("↦", false, "top", R("a")));`
  - `Xtofrom`: `Check("\\xtofrom{a}", GcV("⇄", false, "top", R("a")));`
  - `Xleftrightarrow`: `Check("\\xleftrightarrow{a}", GcV("↔", false, "top", R("a")));`
  - `Xhookrightarrow`: `Check("\\xhookrightarrow{a}", GcV("↪", false, "top", R("a")));`
  - `Xrightarrow_with_complex_labels`: `Check("\\xrightarrow[x+1]{\\alpha}", LL(GcV("→", false, "top", R("α")), R("x+1")));`
- Add new tests for `\overbrace{x}^y`, `\underbrace{x}_y`, legacy `m:limUpp` arrow reading, bracket escaping in optional arrow label, scripted labeled overbrace, and `U+033F` double overbar:
```csharp
[Fact] public void Overbrace_with_superscript_is_limUpp() =>
    Check("\\overbrace{AB}^n", LU(Gc("⏞", true, R("AB")), R("n")));
[Fact] public void Underbrace_with_subscript_is_limLow() =>
    Check("\\underbrace{AB}_{k+1}", LL(Gc("⏟", false, R("AB")), R("k+1")));
[Fact] public void Script_on_labeled_overbrace_is_braced() =>
    Check("{\\overbrace{AB}^n}_k", $"<m:sSub><m:e>{LU(Gc("⏞", true, R("AB")), R("n"))}</m:e><m:sub>{R("k")}</m:sub></m:sSub>");
[Fact] public void Legacy_limUpp_arrow_still_reads_as_xrightarrow() =>
    Assert.Equal("\\xrightarrow[b]{a}", FormulaConverter.OmmlToLatex(OMath(LU(LL(R("→"), R("b")), R("a")))).Output);
[Fact] public void Xrightarrow_below_with_close_bracket_is_braced() =>
    Check("\\xrightarrow[{a]b}]{c}", LL(GcV("→", false, "top", R("c")), R("a]b")));
[Fact] public void Double_overbar_accent_reads_as_nested_overline() =>
    Assert.Equal("\\overline{\\overline{x}}", FormulaConverter.OmmlToLatex(OMath(Ac("\u033F", R("x")))).Output);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AccentTests`
Expected: FAIL on the updated `Xrightarrow*` and new `Overbrace_with_superscript_is_limUpp` / `Double_overbar_accent_reads_as_nested_overline` tests.

- [ ] **Step 3: Implement AST, Parser, OmmlReader/Writer, and LatexWriter changes**

1. `src/FormulaConvert.Core/Ast/FormulaNode.cs`: update `GroupChar` to `internal sealed record GroupChar(string Chr, bool Top, FormulaNode Body, bool VertJcTop = false) : FormulaNode;`.
2. `src/FormulaConvert.Core/Latex/ScriptParsing.cs`: at the end of `Attach`, convert `(GroupChar { Chr: "⏞", Top: true, VertJcTop: false }, null, FormulaNode supNode)` to `new Limit(atom, supNode, true)` and `(GroupChar { Chr: "⏟", Top: false, VertJcTop: false }, FormulaNode subNode, null)` to `new Limit(atom, subNode, false)`.
3. `src/FormulaConvert.Core/Latex/Commands/OverUnderCommands.cs`: for `chr != "="`, build `GroupChar` / `Limit(GroupChar, below, false)` as specified in Interfaces.
4. `src/FormulaConvert.Core/Omml/OmmlWriter.Accents.cs`: in `WriteGroupChar`, when `g.VertJcTop` is true, add `new XElement(M + "vertJc", new XAttribute(M + "val", g.Top ? "bot" : "top"))` after `m:pos`.
5. `src/FormulaConvert.Core/Omml/OmmlReader.Accents.cs`:
   - In `ReadAccent`: if `chr == "\u033F"`, return `new Bar(true, new Bar(true, ReadContent(el.Element(M + "e"), d)))`.
   - In `ReadGroupChar`: read `vertJc = (string?)pr?.Element(M + "vertJc")?.Attribute(M + "val")`; compute `bool top = pos == "top"` and `bool vertJcTop = (!top && vertJc == "top") || (top && vertJc == "bot" && OverUnderCommands.TryGetArrowCommand(chr, out _) && chr != "=")`; return `new GroupChar(chr, top, ReadContent(el.Element(M + "e"), d), vertJcTop)`.
6. `src/FormulaConvert.Core/Latex/LatexWriter.Accents.cs`:
   - In `WriteGroupChar`: if `(g.VertJcTop || AccentCommands.GroupChars.All(x => x.Chr != g.Chr || x.Top != g.Top)) && g.Chr != "=" && OverUnderCommands.TryGetArrowCommand(g.Chr, out var arrowCmd)`: write `arrowCmd`, and if `g.Top` write optional argument `g.Body` (bracing with `HasUnbracedCloseBracket`) plus `{}` else write `{g.Body}`.
   - In `WriteLimit`:
     - If `l is { Upper: true, Base: GroupChar { Chr: "⏞", Top: true, VertJcTop: false } ob }` and `!IsEmpty(l.Lim)`: call `WriteGroupChar(ob, b); WriteScripts(null, l.Lim, b); return;`.
     - If `l is { Upper: false, Base: GroupChar { Chr: "⏟", Top: false, VertJcTop: false } ub }` and `!IsEmpty(l.Lim)`: call `WriteGroupChar(ub, b); WriteScripts(l.Lim, null, b); return;`.
     - If `l is { Upper: false, Base: GroupChar { Top: false } ga } && ga.Chr != "=" && OverUnderCommands.TryGetArrowCommand(ga.Chr, out var gcArrow) && (ga.VertJcTop || AccentCommands.GroupChars.All(x => x.Chr != ga.Chr))`: write `gcArrow` with optional `l.Lim` (using `HasUnbracedCloseBracket`) and `{ga.Body}`.
     - Use `HasUnbracedCloseBracket` for the existing `Limit(Run(arrow), ...)` branch as well.
7. `src/FormulaConvert.Core/Latex/LatexWriter.Scripts.cs`: in `WriteScriptBase`, also brace `Limit` when it is a labeled `\overbrace` or `\underbrace` (`IsLabeledBrace(flat[0])`) so outer scripts do not merge with the brace's own script.
8. Update `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` Task 9 table to reflect `<m:groupChr>` for `\xrightarrow` and `<m:limUpp>`/`<m:limLow>` for `\overbrace{AB}^n`/`\underbrace{AB}_n`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/FormulaConvert.Core/ tests/FormulaConvert.Tests/AccentTests.cs docs/superpowers/plans/2026-10-28-omml-latex-converter.md
git commit -m "feat: use m:groupChr for extensible arrows and m:limUpp/m:limLow for labeled braces"
```

---

### Task 3: Word Functions, N-ary Operators, Accents, PUA Replacement & Missing Symbols

**Files:**
- Modify: `src/FormulaConvert.Core/Data/functions.txt`
- Modify: `src/FormulaConvert.Core/Data/nary.csv`
- Modify: `src/FormulaConvert.Core/Data/accents.csv`
- Modify: `src/FormulaConvert.Core/Data/outofscope.csv`
- Modify: `src/FormulaConvert.Core/Data/aliases.csv`
- Modify: `src/FormulaConvert.Core/Data/symbols.csv`
- Modify: `tools/extract_symbols.py`
- Test: `tests/FormulaConvert.Tests/DataTableTests.cs`
- Test: `tests/FormulaConvert.Tests/NaryFunctionTests.cs`
- Test: `tests/FormulaConvert.Tests/AccentTests.cs`

**Interfaces:**
- Produces:
  - `functions.txt`: adds `\sech`, `\csch`.
  - `nary.csv`: adds `\intclockwise,2231,true`, `\varointclockwise,2232,true`, `\ointctrclockwise,2233,true`, `\intctrclockwise,2A11,true`.
  - `accents.csv`: adds `\overleftharpoon,20D0`, `\overrightharpoon,20D1` (removed from `outofscope.csv`).
  - `aliases.csv`: removes `\ratio,0,:` (moved to `symbols.csv` as `\ratio,2236`).
  - `symbols.csv`: replaces all 14 `E0xx` PUA entries (`\nshortmid,2224`, `\nshortparallel,2226`, `\lvertneqq,2268`, `\gvertneqq,2269`, `\ngeqq,2271`, `\ngeqslant,2A88`, `\nleqslant,2A87`, `\nleqq,2270`, `\nsubseteqq,2288`, `\varsubsetneqq,2ACB`, `\nsupseteqq,2289`, `\varsupsetneqq,2ACC`, `\varsubsetneq,228A`, `\varsupsetneq,228B`) and appends Word symbols (`\iddots,22F0`, `\nequiv,2262`, `\nsimeq,2244`, `\ncong,2247`, `\napprox,2249`, `\nasymp,226D`, `\ntlg,2278`, `\ntgl,2279`, `\nsqsubseteq,22E2`, `\nsqsupseteq,22E3`, `\rightangle,221F`, `\measuredrightangle,22BE`, `\varlrtriangle,22BF`, `\epar,22D5`, `\ratio,2236`, `\qed,220E`, `\AA,212B`, `\Euler,2107`, `\dotminus,2238`, `\dlsh,21B2`, `\drsh,21B3`, `\leftwavearrow,219C`, `\rightwavearrow,219D`, keeping `2246` mapped after `2247` or via `\ncong,2246` secondary row so `TryGetCommand(0x2247)` returns `\ncong` and `TryGetCodepoint("\\ncong")` returns `0x2247`).

- [ ] **Step 1: Write the failing tests in `DataTableTests.cs`, `NaryFunctionTests.cs`, and `AccentTests.cs`**

In `tests/FormulaConvert.Tests/DataTableTests.cs`, add:
```csharp
[Fact]
public void No_symbol_uses_private_use_area()
{
    foreach (var r in CsvResource.ReadRows("symbols.csv"))
    {
        int cp = CsvResource.ParseHex(r[1]);
        Assert.False(cp is >= 0xE000 and <= 0xF8FF, $"{r[0]} uses PUA codepoint U+{cp:X4}");
    }
}

[Theory]
[InlineData("\\iddots", 0x22F0)]
[InlineData("\\nequiv", 0x2262)]
[InlineData("\\rightangle", 0x221F)]
[InlineData("\\ratio", 0x2236)]
[InlineData("\\qed", 0x220E)]
[InlineData("\\AA", 0x212B)]
[InlineData("\\dotminus", 0x2238)]
[InlineData("\\dlsh", 0x21B2)]
[InlineData("\\drsh", 0x21B3)]
public void Word_gallery_symbols_exist(string cmd, int cp)
{
    Assert.True(SymbolTable.TryGetCodepoint(cmd, out var got));
    Assert.Equal(cp, got);
}
```

In `tests/FormulaConvert.Tests/NaryFunctionTests.cs`, add:
```csharp
[Theory]
[InlineData("\\sech x", "sech")]
[InlineData("\\csch x", "csch")]
public void Hyperbolic_sech_and_csch(string latex, string name) =>
    Check(latex, Fn(FName(name), R("x")));

[Theory]
[InlineData("\\intclockwise_C f", "∱")]
[InlineData("\\varointclockwise_C f", "∲")]
[InlineData("\\ointctrclockwise_C f", "∳")]
public void Oriented_integrals_are_nary(string latex, string chr) =>
    Check(latex, N(chr, R("C"), null, R("f")));
```

In `tests/FormulaConvert.Tests/AccentTests.cs`, add `[InlineData("\\overleftharpoon", "⃐")]` and `[InlineData("\\overrightharpoon", "⃑")]` to `Accent_commands`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~No_symbol_uses_private_use_area|FullyQualifiedName~Word_gallery_symbols_exist|FullyQualifiedName~Hyperbolic_sech_and_csch|FullyQualifiedName~Oriented_integrals_are_nary"`
Expected: FAIL

- [ ] **Step 3: Update `functions.txt`, `nary.csv`, `accents.csv`, `outofscope.csv`, `aliases.csv`, `symbols.csv`, and `tools/extract_symbols.py`**

Apply all table updates listed in the Task 3 Interfaces block. In `SymbolTable.cs`, ensure `TryGetCodepoint` uses `TryAdd` (first occurrence wins) so `\ncong,2247` followed by `\ncong,2246` maps `\ncong` → `0x2247` while reverse lookup maps both `0x2247` and `0x2246` to `\ncong`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/FormulaConvert.Core/Data/ tools/extract_symbols.py tests/FormulaConvert.Tests/DataTableTests.cs tests/FormulaConvert.Tests/NaryFunctionTests.cs tests/FormulaConvert.Tests/AccentTests.cs
git commit -m "feat: add missing Word symbols/functions/nary/accents and replace PUA codepoints"
```

---

### Task 4: Unicode Minus (`U+2212`) and Mathematical Alphanumeric Symbols (`MathAlphaDecoder`)

**Files:**
- Create: `src/FormulaConvert.Core/Omml/MathAlphaDecoder.cs`
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.cs:69-72`
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.Styles.cs:12-52`
- Modify: `src/FormulaConvert.Core/Latex/LatexParser.cs:294-299`
- Modify: `docs/superpowers/plans/2026-10-28-omml-latex-converter.md`
- Test: `tests/FormulaConvert.Tests/StyleTests.cs`
- Test: `tests/FormulaConvert.Tests/CoreTests.cs`
- Test: `tests/FormulaConvert.Tests/SymbolSweepTests.cs`

**Interfaces:**
- Produces:
  - `internal static class MathAlphaDecoder { public static bool TryDecode(Rune rune, out char ascii, out RunStyle style); }`
    - Decodes Mathematical Alphanumeric Symbols (`U+1D400..U+1D6A3`, `U+1D7CE..U+1D7FF`) and BMP Letterlike styled letters:
      - Double-Struck (`Scr.DoubleStruck`, `Sty.Italic`): `ℂ` (`2102`), `ℕ` (`2115`), `ℙ` (`2119`), `ℚ` (`211A`), `ℝ` (`211D`), `ℤ` (`2124`)
      - Script (`Scr.Script`, `Sty.Italic`): `ℬ` (`212C`), `ℰ` (`2130`), `ℱ` (`2131`), `ℋ` (`210B`), `ℐ` (`2110`), `ℒ` (`2112`), `ℳ` (`2133`), `ℛ` (`211B`), `ℯ` (`212F`), `ℊ` (`210A`), `ℴ` (`2134`)
      - Fraktur (`Scr.Fraktur`, `Sty.Italic`): `ℭ` (`212D`), `ℌ` (`210C`), `ℨ` (`2128`)
      - Italic `h`: `ℎ` (`210E` → `'h'`, `RunStyle(Sty.Italic, Scr.Roman)`)
    - Does NOT decode BMP symbols that have their own symbol commands in `symbols.csv` (`\hbar` `210F`, `\Im` `2111`, `\ell` `2113`, `\wp` `2118`, `\Re` `211C`, `\mho` `2127`, `\AA` `212B`, `\Finv` `2132`, `\aleph` `2135`..`\daleth` `2138`, `\Euler` `2107`).
  - `OmmlReader.ReadRun(XElement r, List<FormulaNode> items, List<Diagnostic> d)`:
    - For `<m:nor/>` runs, adds one `Run(text, new RunStyle(Normal: true))` untouched.
    - For math runs, replaces `−` (`U+2212`) with `-` (`U+002D`), decodes any rune matching `MathAlphaDecoder.TryDecode` (combining with explicit `m:sty` / `m:scr` on `m:rPr` when present), and merges consecutive characters of equal `RunStyle`.
  - `LatexParser`: in `ParseAtom` for `TokenKind.Char`, normalizes `−` (`U+2212`) to `-` and decodes `MathAlphaDecoder.TryDecode` into `new Run(ascii.ToString(), style)`.

- [ ] **Step 1: Write the failing tests in `StyleTests.cs`, `CoreTests.cs`, and `SymbolSweepTests.cs`**

In `tests/FormulaConvert.Tests/StyleTests.cs`:
- Replace `Math_alphanumerics_pass_through` with:
```csharp
[Theory]
[InlineData("\U0001D400", "\\mathbf{A}")]
[InlineData("\U0001D49C", "\\mathcal{A}")]
[InlineData("\U0001D504", "\\mathfrak{A}")]
[InlineData("\U0001D538", "\\mathbb{A}")]
[InlineData("\U0001D4B6", "\\mathcal{a}")]
[InlineData("\U0001D51E", "\\mathfrak{a}")]
[InlineData("\U0001D552", "\\mathbb{a}")]
[InlineData("ℝ", "\\mathbb{R}")]
[InlineData("ℂ", "\\mathbb{C}")]
[InlineData("ℋ", "\\mathcal{H}")]
[InlineData("ℌ", "\\mathfrak{H}")]
public void Math_alphanumerics_decode_to_styled_runs(string ch, string expectedLatex)
{
    Assert.Equal(expectedLatex, Read(R(ch), out var d));
    Assert.Empty(d);
    Assert.Equal(expectedLatex, FormulaConverter.OmmlToLatex(FormulaConverter.LatexToOmml(ch).Output).Output);
}

[Fact]
public void Explicit_rPr_combines_with_decoded_math_alphanumeric()
{
    Assert.Equal("\\mathbf{\\mathbb{R}}", Read(R("ℝ", Sty("b")), out var d));
    Assert.Empty(d);
}

[Fact]
public void Normal_text_keeps_math_alphanumerics_and_unicode_minus_literal()
{
    Check("\\text{ℝ−𝐀}", R("ℝ−𝐀", Nor));
}
```

In `tests/FormulaConvert.Tests/CoreTests.cs`:
- Update `Non_bmp_passthrough` to use a non-math non-BMP character (`𝄞` `U+1D11E`):
```csharp
[Fact] public void Non_bmp_passthrough() => Check("𝄞+é中", R("𝄞+é中"));
[Fact] public void Unicode_minus_normalizes_to_ascii_hyphen() => Check("a−b", R("a-b"), "a-b");
```

In `tests/FormulaConvert.Tests/SymbolSweepTests.cs`:
- Update `Unlisted_character_passes_through` line 56 from `[InlineData("𝐀")]` to `[InlineData("𝄞")]`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Math_alphanumerics_decode_to_styled_runs|FullyQualifiedName~Unicode_minus_normalizes_to_ascii_hyphen"`
Expected: FAIL

- [ ] **Step 3: Implement `MathAlphaDecoder.cs` and wire it into `OmmlReader.Styles.cs` and `LatexParser.cs`**

1. Create `src/FormulaConvert.Core/Omml/MathAlphaDecoder.cs` implementing `TryDecode(Rune rune, out char ascii, out RunStyle style)` for all contiguous ranges in `U+1D400..U+1D6A3` (Latin upper/lower Bold, Italic, BoldItalic, Script, BoldScript, Fraktur, DoubleStruck, BoldFraktur, SansSerif, SansSerifBold, SansSerifItalic, SansSerifBoldItalic, Monospace), `U+1D7CE..U+1D7FF` (digits Bold, DoubleStruck, SansSerif, SansSerifBold, Monospace), and the BMP Letterlike exceptions listed in Interfaces.
2. Update `OmmlReader.Styles.cs` and `OmmlReader.cs` so `ReadRun` appends decoded/merged `Run` items to `items` (converting `\u2212` to `'-'` in non-normal runs).
3. Update `LatexParser.ParseAtom` so `TokenKind.Char` converts `\u2212` to `"-"` and decodes `MathAlphaDecoder.TryDecode` runes into `new Run(ascii.ToString(), style)`.
4. Update `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` (Review Focus item 5 & Task 3 `Non_bmp_passthrough`) to note `𝄞+é中` for non-math non-BMP passthrough and `MathAlphaDecoder` for math alphanumerics.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: PASS (all tests green)

- [ ] **Step 5: Commit**

```bash
git add src/FormulaConvert.Core/ tests/FormulaConvert.Tests/ docs/superpowers/plans/2026-10-28-omml-latex-converter.md
git commit -m "feat: decode Unicode Mathematical Alphanumerics and normalize U+2212 minus"
```
