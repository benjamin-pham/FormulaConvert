# OMML ↔ LaTeX (MathLive) Converter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Console app .NET chuyển hai chiều OMML ↔ LaTeX (cú pháp MathLive), chỉ phần giao của hai bên.

**Architecture:** Cây trung gian (AST) dùng chung. `LatexParser → AST → OmmlWriter` và `OmmlReader → AST → LatexWriter`. Mỗi nhóm tính năng là một lát cắt dọc (parser handler + 3 phần partial của reader/writer) có bảng test hai chiều riêng.

**Tech Stack:** C#, .NET 10 (SDK 10.0.113 đã có trên máy), `System.Xml.Linq`, xUnit (chỉ cho test), dữ liệu CSV nhúng.

**Spec:** `docs/superpowers/specs/2026-10-28-omml-latex-converter-design.md`

## Global Constraints

- .NET 10, C#. Ba project: `src/FormulaConvert.Core` (class library), `src/FormulaConvert.Cli` (console), `tests/FormulaConvert.Tests` (xUnit). Không phụ thuộc gói ngoài, trừ xUnit cho test.
- Namespace OMML: `http://schemas.openxmlformats.org/officeDocument/2006/math`, prefix `m`.
- CLI: `formulaconvert omml2latex [file]` và `formulaconvert latex2omml [file]`; không có file thì đọc stdin; kết quả ra stdout, cảnh báo (`Diagnostic`) ra stderr; mã thoát 0 khi thành công (kể cả có cảnh báo), 1 khi lỗi cú pháp/đầu vào.
- Lệnh ngoài phạm vi (màu, cỡ chữ, `\href`, `\class`, `\cssId`, `\htmlData`, `\texttip`, `\mathtip`, `\error`, `\ce{}`, khoảng trắng, `\displaystyle` và các lệnh style, họ font, macro/primitive/register TeX): bỏ lệnh bọc ngoài, **giữ nội dung bên trong**, ghi `Diagnostic`.
- `\dfrac`/`\tfrac` đọc được nhưng ra `\frac` kèm cảnh báo (OMML không phân biệt).
- Round-trip: `LaTeX → OMML → LaTeX` bằng nhau **sau chuẩn hóa** (dạng LaTeX chuẩn bên dưới).
- Dữ liệu ký hiệu lấy từ mã nguồn MathLive (MIT, gói npm `mathlive` 0.111.1): file `THIRD-PARTY-NOTICES.md` ở gốc repo phải chép nguyên thông báo bản quyền của MathLive.
- Thư mục chưa phải git repo: **không có bước commit**. Mỗi task kết thúc khi toàn bộ test (`dotnet test`) xanh.

## Quy ước do kế hoạch này chốt (spec để ngỏ)

- **OMML tối giản:** bỏ phần tử thuộc tính khi mọi giá trị là mặc định (`fPr` khi `bar`, `radPr` chỉ khi ẩn bậc, v.v.). Reader chấp nhận cả dạng đầy đủ do Word sinh.
- **Run:** parser sinh một `Run` mỗi ký tự; `OmmlWriter` gộp các `Run` kề nhau cùng `RunStyle` thành một `m:r`/`m:t`. Reader giữ nguyên `Run` nhiều ký tự.
- **LaTeX chuẩn:** các mục nối liền không dấu cách; chèn đúng một dấu cách sau lệnh kết thúc bằng chữ cái khi ký tự kế tiếp là chữ cái ASCII (`\alpha x`); đối số `\frac`/`\sqrt`/`\binom` luôn có `{}`; đối số chỉ số bỏ `{}` khi chỉ là một ký tự hoặc một lệnh ký hiệu (`x^2`, `x_i`, `x^\alpha`), còn lại có `{}` (`x^{ab}`); cơ sở của chỉ số gồm nhiều mục thì bọc `{ab}^2`; ký tự đặc biệt trong `Run` thoát thành `\# \$ \% \& \_ \{ \}`, `\` thành `\backslash`.
- **Dấu cách bổ sung:** sau một đối số chỉ số **không bọc `{}`** (`x^2`, `\log_2`), nếu ký tự phát ra kế tiếp là chữ cái hoặc chữ số ASCII thì chèn một dấu cách (`\sum_{i=0}^n a_i`, `\log_2 8`, `{}_a^b x`); sau đối số bọc `{}` thì không chèn.
- **Ký tự ASCII** in được trong `Run` luôn viết nguyên văn (kèm bộ thoát bên trên); chỉ tra `SymbolTable.TryGetCommand` cho codepoint ≥ U+0080. Riêng bộ thoát ngoặc của `Delimited` tự ánh xạ `{`→`\lbrace`, `}`→`\rbrace`, `|`→`|`, `‖`→`\Vert`.
- **Mũi tên và ký hiệu chuẩn** theo `TryGetCommand` (ví dụ `→` luôn ra `\rightarrow`, `≠` ra `\ne`), nên mọi ô Canonical trong bảng test phải viết bằng dạng chuẩn đó (`\lim_{x\to0} f` có Canonical `\lim_{x\rightarrow0}f`).
- **Ô Canonical để trống** nghĩa là "bằng đúng output của writer theo các quy tắc ở mục này". Nếu cột Latex có dấu cách mà writer không phát ra, hoặc thứ tự `\limits` khác, người triển khai tự suy Canonical theo quy tắc (xác định, không tùy ý) và ghi lại vào báo cáo; ô Canonical đã điền có hiệu lực, trừ khi mâu thuẫn với quy tắc ở mục này; khi mâu thuẫn thì quy tắc thắng và test dùng giá trị suy theo quy tắc. `\limits`/`\nolimits` được writer đặt ngay sau toán tử (`\int\limits_0^1 f`).
- **Dấu phẩy trên đơn** luôn bọc `{}` (`f^{\prime}`), ngoại lệ của quy tắc "bỏ `{}` khi một lệnh ký hiệu".
- **Toán hạng** của `Nary` và `FunctionApply` là **atom kế tiếp** (nhóm `{…}`, một ký tự, hoặc một lệnh kèm chỉ số gắn liền); riêng đối số hàm bắt đầu bằng `(` hoặc `[` kéo dài đến ký tự đóng tương ứng. Writer bọc `{}` khi toán hạng nhiều mục (trừ đối số hàm là một chuỗi `Run` mở bằng `(` và đóng bằng `)`, ví dụ `\sin(x)`), nên parse lại ổn định.
- `\binom` ↔ `Delimited("(", ")", "", [Frac(…, NoBar)])`; `\atop` ↔ `Frac NoBar`; `\choose` ↔ `\binom`.
- Dấu `'` ↔ chỉ số trên `\prime` (U+2032); writer ra `^{\prime}`.
- Ký tự `&` trong `align`/`gather`… giữ nguyên là `Run("&")` bên trong hàng của `EqArray` (đúng cách OMML đánh dấu điểm căn).
- `Delimited` mất cỡ của `\bigl(` (ra ký tự trần, kèm cảnh báo `lossy`).
- Mã `Diagnostic.Code`: `out-of-scope`, `lossy`, `unsupported-omml`.

## Review Focus

1. Ký tự đặc biệt của LaTeX nằm trong `m:t` (`#`, `$`, `%`, `&`, `_`, `{`, `}`, `\`) phải thoát đúng khi ra LaTeX và đọc lại được (test ở Task 3).
2. Nhóm rỗng và đối số rỗng (`{}`, `\frac{}{}`, `^2` không có cơ sở, placeholder `\unicode{"2B1A}`) không được làm sập chương trình (Task 3, 4, 5).
3. Đầu vào hỏng (thiếu `}`, `\left` không có `\right`, `\end` sai tên, hai `^` liên tiếp, lệnh lạ) phải cho `FormulaParseException` có vị trí, không xuất kết quả dở dang, CLI thoát mã 1 (Task 3, 5, 7, 8, 13).
4. OMML thực tế từ Word có nhiễu: `m:ctrlPr`, `w:rPr` trong `m:r`, `m:oMathPara`, `xml:space="preserve"`, nút văn bản chỉ có khoảng trắng; không được sinh ký tự rác hay sập (Task 3).
5. Ký tự ngoài BMP (cặp surrogate, ví dụ 𝐀) và ký tự không có lệnh LaTeX (é, 中) phải đi qua nguyên vẹn hai chiều (Task 3).

---

## Task 1: Khung solution và mô hình AST

**Files:**
- Create: `FormulaConvert.sln`, `src/FormulaConvert.Core/FormulaConvert.Core.csproj`, `src/FormulaConvert.Cli/FormulaConvert.Cli.csproj`, `tests/FormulaConvert.Tests/FormulaConvert.Tests.csproj`
- Create: `src/FormulaConvert.Core/Ast/FormulaNode.cs` (toàn bộ kiểu nút), `src/FormulaConvert.Core/Diagnostics.cs`
- Create: `THIRD-PARTY-NOTICES.md` (tạm có tiêu đề; Task 2 điền nội dung)
- Test: `tests/FormulaConvert.Tests/ScaffoldTests.cs`

**Interfaces:**
- Produces (namespace `FormulaConvert.Ast`, tất cả `public sealed record … : FormulaNode`, `FormulaNode` là `public abstract record`):
  - `FormulaRow(IReadOnlyList<FormulaNode> Items)` với `static FormulaRow Empty`
  - `Run(string Text, RunStyle Style)`; `RunStyle(Sty Sty = Sty.Italic, Scr Scr = Scr.Roman, bool Normal = false)`; `enum Sty { Italic, Plain, Bold, BoldItalic }`; `enum Scr { Roman, Script, Fraktur, DoubleStruck, SansSerif, Monospace }`
  - `Frac(FormulaNode Num, FormulaNode Den, FracKind Kind)`; `enum FracKind { Bar, NoBar, Lin }`
  - `Radical(FormulaNode? Degree, FormulaNode Body)`
  - `Script(FormulaNode Base, FormulaNode? Sub, FormulaNode? Sup)`; `PreScript(FormulaNode Base, FormulaNode? Sub, FormulaNode? Sup)`
  - `Nary(string Chr, FormulaNode? Sub, FormulaNode? Sup, FormulaNode Body, bool SubSupLimits)`
  - `Delimited(string Open, string Close, string Separator, IReadOnlyList<FormulaNode> Parts)` (chuỗi rỗng = không có ký tự)
  - `Matrix(IReadOnlyList<IReadOnlyList<FormulaNode>> Rows, string? ColumnAlign)` (ký tự `l`/`c`/`r`)
  - `EqArray(IReadOnlyList<FormulaNode> Rows)`
  - `Accent(string Chr, FormulaNode Body)`; `Bar(bool Top, FormulaNode Body)`; `GroupChar(string Chr, bool Top, FormulaNode Body)`
  - `Limit(FormulaNode Base, FormulaNode Lim, bool Upper)`
  - `FunctionApply(FormulaNode Name, FormulaNode Arg)`
  - `BorderBox(BoxEdges Edges, FormulaNode Body)`; `[Flags] enum BoxEdges { None=0, Top=1, Bottom=2, Left=4, Right=8, StrikeH=16, StrikeV=32, StrikeUp=64, StrikeDown=128 }`
  - `Phantom(bool Show, bool ZeroWidth, bool ZeroAscent, bool ZeroDescent, FormulaNode Body)`
- Produces (namespace `FormulaConvert`): `public sealed record Diagnostic(string Code, string Message, int Position)`; `public sealed record ConvertResult(string Output, IReadOnlyList<Diagnostic> Diagnostics)`; `public sealed class FormulaParseException : Exception` với `FormulaParseException(string message, int position)` và `int Position`.

- [ ] **Step 1: Scaffold** — `dotnet new sln`, tạo ba project (`classlib`, `console`, `xunit`) trong thư mục ở trên, `net10.0`, Cli tham chiếu Core, Tests tham chiếu Core, Core có `<InternalsVisibleTo Include="FormulaConvert.Tests" />`, `<Nullable>enable</Nullable>`; assembly name của Cli là `formulaconvert`.
- [ ] **Step 2: Write the failing test** `ScaffoldTests.Frac_records_expose_parts`: tạo `new Frac(new Run("a", new RunStyle()), new Run("b", new RunStyle()), FracKind.Bar)`, assert `Kind == FracKind.Bar` và `((Run)f.Num).Text == "a"`; `FormulaRow.Empty.Items` rỗng; `new FormulaParseException("x", 3).Position == 3`.
- [ ] **Step 3:** `dotnet test` → FAIL (kiểu chưa có). Cài các kiểu ở Interfaces.
- [ ] **Step 4:** `dotnet build FormulaConvert.sln` và `dotnet test` → PASS.

---

## Task 2: Dữ liệu ký hiệu và các bảng tra

**Files:**
- Create: `tools/extract_symbols.py` (công cụ dev, không thuộc build)
- Create (EmbeddedResource) trong `src/FormulaConvert.Core/Data/`: `symbols.csv` (`command,codepoint_hex`), `mathlive-commands.txt` (721 lệnh trần của tài liệu, mỗi dòng một lệnh), `functions.txt`, `nary.csv` (`command,codepoint_hex,subsup_default`), `accents.csv` (`command,codepoint_hex`), `aliases.csv` (`command,arity,replacement`), `outofscope.csv` (`command,arity,kept_arg_index`; `-1` = bỏ hết)
- Create: `src/FormulaConvert.Core/Data/SymbolTable.cs`, `FunctionTable.cs`, `NaryTable.cs`, `AccentTable.cs`, `AliasTable.cs`, `OutOfScopeTable.cs`, `CsvResource.cs`
- Modify: `THIRD-PARTY-NOTICES.md`
- Test: `tests/FormulaConvert.Tests/DataTableTests.cs`

**Interfaces:**
- Produces (`internal static`, namespace `FormulaConvert.Data`; tên lệnh luôn kèm dấu `\`, ví dụ `"\\alpha"`):
  - `SymbolTable.TryGetCodepoint(string command, out int codepoint)`; `SymbolTable.TryGetCommand(int codepoint, out string command)` (trả lệnh chuẩn = dòng đầu tiên của codepoint trong `symbols.csv`)
  - `FunctionTable.Contains(string command)`; `FunctionTable.BoundsFunctions` (tập lệnh có giới hạn dưới: `\lim \liminf \limsup \max \min \sup \inf \injlim \projlim \varliminf \varlimsup \varinjlim \varprojlim`)
  - `NaryTable.TryGetByCommand(string, out NaryInfo)`; `NaryTable.TryGetByChar(string chr, out NaryInfo)`; `record NaryInfo(string Command, string Chr, bool SubSupDefault)`
  - `AccentTable.TryGetByCommand(string, out string chr)`; `AccentTable.TryGetByChar(string chr, out string command)`
  - `AliasTable.TryGet(string command, out (int Arity, string Replacement) alias)` (thay thế dùng `#1`, `#2`)
  - `OutOfScopeTable.TryGet(string command, out (int Arity, int KeptArg) rule)`

- [ ] **Step 1: Write the failing tests** (`DataTableTests`):
  - `Symbol_pairs_match_unicode`: `\alpha→03B1`, `\leq→2264`, `\rightarrow→2192`, `\forall→2200`, `\infty→221E`, `\partial→2202`, `\in→2208`, `\cup→222A`, `\times→00D7`, `\Omega→03A9`, `\hbar→210F`, `\therefore→2234`, `\nabla→2207`, `\emptyset→2205`.
  - `Reverse_lookup_prefers_canonical`: `TryGetCommand(0x2264)` = `\leq`; `0x2192` = `\rightarrow`; `0x2260` = `\ne`; `0x2265` = `\geq`.
  - `Nary_table`: `\sum→2211 (SubSupDefault false)`, `\int→222B (true)`, `\oint→222E (true)`, `\prod→220F`, `\bigcup→22C3`, `\bigoplus→2A01`.
  - `Accent_table`: `\hat→0302`, `\tilde→0303`, `\vec→20D7`, `\dot→0307`, `\ddot→0308`, `\bar→0304`, `\check→030C`, `\breve→0306`, `\mathring→030A`.
  - `Functions`: `FunctionTable.Contains("\\sin")`, `"\\arcsin"`, `"\\lim"`, `"\\gcd"`; `BoundsFunctions` chứa `\lim`, không chứa `\sin`.
  - `Symbols_are_at_least_480`: số dòng `symbols.csv` ≥ 480.
- [ ] **Step 2:** Run `dotnet test --filter DataTableTests` → FAIL.
- [ ] **Step 3: `tools/extract_symbols.py`.** Tải `https://mathlive.io/mathfield/reference/commands/` và gói `mathlive` 0.111.1 (`npm pack`), giải nén vào thư mục riêng, **không thực thi mã trong gói**. Từ `mathlive.mjs` lấy: (a) cặp `["\\cmd", N]` bằng regex `\[\s*"(\\\\[A-Za-z]+)"\s*,\s*(\d+)`; (b) bảng codepoint→lệnh chuẩn `^\s*(\d+): "(\\\\[A-Za-z]+)"` (dùng để xếp lệnh chuẩn lên đầu mỗi codepoint). Từ trang tài liệu (chuyển HTML sang text) lấy các dòng chỉ có đúng một lệnh `^\\[A-Za-z]+$` → `mathlive-commands.txt`. Ghi `symbols.csv` (codepoint hex 4+ chữ số in hoa). Chạy script, rồi **soạn tay** các CSV còn lại từ phân nhóm của tài liệu: `functions.txt` = mục Functions, Trigonometry, Non-Standard Trig, Bounds, Projections (gồm cả `\Pr`, `\hom`; không gồm `\degree`, `\ang`); `nary.csv` = `\sum \prod \coprod \int \iint \iiint \oint \oiint \oiiint \intop \smallint \bigcup \bigcap \bigvee \bigwedge \biguplus \bigotimes \bigoplus \bigodot \bigsqcup` với codepoint Unicode tương ứng (∫-họ `subsup_default=true`, còn lại `false`); `accents.csv` theo bảng test; `aliases.csv` cho bí danh/macro (ví dụ `\neq→\ne`, `\iff→\Longleftrightarrow`, `\R→\mathbb{R}`, `\N`, `\Z`, `\Q`, `\C`, `\reals`, `\set`, `\Set`, `\rd→\mathrm{d}`, `\imaginaryI`, `\exponentialE`, `\differentialD`, các `\scriptCapital*`/`\gothicCapital*`/`\doubleStruckCapital*`, `\darr`-họ của MediaWiki) và các chữ Hy Lạp hoa (`\Alpha`…, thêm vào `symbols.csv` với codepoint U+0391…); `outofscope.csv` được điền ở Task 12. Chép nguyên thông báo bản quyền MIT của MathLive vào `THIRD-PARTY-NOTICES.md`.
- [ ] **Step 4: Implement các bảng** theo Interfaces; nạp CSV từ resource nhúng một lần (`Lazy`), bỏ qua dòng trống và dòng bắt đầu bằng `#`.
- [ ] **Step 5:** `dotnet test` → PASS.

---

## Task 3: Đường ống lõi (tokenizer, parser, reader/writer của Run, API công khai)

**Files:**
- Create: `src/FormulaConvert.Core/Latex/LatexTokenizer.cs`, `LatexParser.cs`, `CommandTable.cs`, `Commands/SymbolCommands.cs`, `LatexWriter.cs`
- Create: `src/FormulaConvert.Core/Omml/OmmlReader.cs`, `OmmlWriter.cs`
- Create: `src/FormulaConvert.Core/FormulaConverter.cs`
- Test: `tests/FormulaConvert.Tests/Harness/Xml.cs`, `Harness/ConversionCase.cs`, `Harness/CaseRunner.cs`, `CoreTests.cs`, `SymbolSweepTests.cs`

**Interfaces:**
- Consumes: Task 1 (AST, `Diagnostic`, `FormulaParseException`), Task 2 (`SymbolTable`, `AliasTable`).
- Produces:
  - `public static class FormulaConverter { public static ConvertResult LatexToOmml(string latex); public static ConvertResult OmmlToLatex(string omml); }`. `LatexToOmml` trả `<m:oMath xmlns:m="…">…</m:oMath>` không thụt dòng (`SaveOptions.DisableFormatting`). `OmmlToLatex` nhận `m:oMath`, `m:oMathPara`, hoặc XML có nhiều `m:oMath` (kết quả nối bằng `\n`); XML hỏng hoặc không có `m:oMath` → `FormulaParseException` (position `-1`).
  - `internal delegate FormulaNode CommandHandler(LatexParser p, string name);` và `internal delegate FormulaNode InfixHandler(LatexParser p, string name, List<FormulaNode> left);`
  - `internal sealed class CommandTable { Add(string name, CommandHandler h); AddInfix(string name, InfixHandler h); bool Has(string name); static CommandTable Default }` — `Default` gọi `XxxCommands.Register(table)` của từng nhóm.
  - `internal sealed class LatexParser`: `static FormulaNode Parse(string latex, List<Diagnostic> diagnostics)`; thành viên cho handler: `FormulaNode ReadArgument()` (nhóm `{…}` hoặc một atom; thiếu → lỗi), `FormulaNode? ReadOptionalArgument()` (`[…]`), `string ReadRawGroup()` (văn bản thô trong `{…}`), `FormulaNode ParseRowRest()` (đọc đến hết hàng hiện tại), `int Position`, `List<Diagnostic> Diagnostics`, `FormulaParseException Error(string message)`.
  - `internal static class OmmlReader { static FormulaNode Read(XElement root, List<Diagnostic> d); }`, `internal static class OmmlWriter { static XElement Write(FormulaNode root); }`, `internal static class LatexWriter { static string Write(FormulaNode root); }`; mỗi cái dùng `switch` theo kiểu nút, các task sau thêm `partial class` riêng (`OmmlWriter.<Nhóm>.cs`…).
  - Test harness: `static class Xml { string R(string text, string? rPr = null) }` trả `<m:r>{rPr}<m:t>{text}</m:t></m:r>` (`rPr` bọc trong `<m:rPr>`); `record ConversionCase(string Latex, string OmmlInner, string? CanonicalLatex = null)`; `CaseRunner.Check(ConversionCase c)` thực hiện ba kiểm tra: (a) `LatexToOmml(c.Latex)` bằng `<m:oMath>` + `OmmlInner` (so sánh sau khi parse, bỏ nút văn bản chỉ có khoảng trắng); (b) `OmmlToLatex(OmmlInner đã bọc)` bằng `CanonicalLatex ?? Latex`; (c) `LatexToOmml(OmmlToLatex(...))` trả lại đúng OMML ban đầu.

- [ ] **Step 1: Write the failing tests** (`CoreTests`, mỗi dòng dùng `CaseRunner.Check`, `R` là `Xml.R`):

| Test | Latex | OmmlInner | Canonical |
|---|---|---|---|
| `Letters_merge_into_one_run` | `a+b=1` | `R("a+b=1")` | |
| `Spaces_ignored` | `a + b` | `R("a+b")` | `a+b` |
| `Greek_symbol` | `\alpha x` | `R("αx")` | |
| `Group_is_transparent` | `{a}b` | `R("ab")` | `ab` |
| `Empty_input` | `` | `` (rỗng) | |
| `Empty_group` | `{}` | `` | `` |
| `Unicode_command` | `\unicode{"2B1A}` | `R("⬚")` | `⬚` |
| `Char_and_caret_forms` | `\char"2A17`, `^^41`, `^^^^2a17` | `R("⨗")`, `R("A")`, `R("⨗")` | `⨗`, `A`, `⨗` |
| `Escaped_specials` | `\#\$\%\&\_\{\}` | `R("#$%&_{}")` | cùng chuỗi |
| `Backslash_symbol` | `\backslash` | `R("\")` | |
| `Non_bmp_passthrough` | `𝐀+é中` | `R("𝐀+é中")` | |

  Thêm ngoài bảng: `Unknown_command_throws` (`\foo` → `FormulaParseException`, `Position == 0`); `Unbalanced_brace_throws` (`{a` và `a}` → exception, position đúng ký tự lỗi); `Reader_ignores_word_noise`: OMML `<m:oMathPara><m:oMath><m:r><w:rPr/><m:rPr><m:sty m:val="p"/></m:rPr><m:t xml:space="preserve">x</m:t></m:r><m:ctrlPr/>` kèm khoảng trắng giữa các thẻ, khai báo thêm namespace `w` → `OmmlToLatex` ra đúng `x` (Task 3 chưa đọc kiểu chữ; Task 11 sửa assert này thành `\mathrm{x}`); `Unknown_omml_element_warns`: phần tử `m:foo` chứa `R("a")` → output `a`, có một `Diagnostic` mã `unsupported-omml`; `Multiple_omath_joined_by_newline`; `Malformed_xml_throws` (Position `-1`).
  `SymbolSweepTests` (Theory duyệt toàn bộ `symbols.csv`): `LatexToOmml(command)` chứa đúng ký tự codepoint trong `m:t`; `OmmlToLatex(R(char))` trả lệnh chuẩn của codepoint (với ký tự tự do ngoài bảng, trả lại chính ký tự).
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3: Implement.** Tokenizer tách: lệnh chữ (`\[A-Za-z]+`), lệnh một ký tự, `{ } [ ] ^ _ & '`, `\\`, `%…hết dòng` (bỏ), khoảng trắng (bỏ), ký tự thường (giữ code point nguyên cặp surrogate). Parser: `ParseRow` lặp đọc atom đến `}` / EOF (các ngữ cảnh sau thêm `\right`, `\end`, `&`, `\\`); `{…}` → `FormulaRow` (hàng một mục được rút gọn thành chính mục đó); ký tự → `Run`; `\x` thoát (`\# \$ \% \& \_ \{ \}`) → `Run` của ký tự; `\unicode{…}` (thập phân, hoặc bắt đầu `x`/`"` là thập lục phân), `\char"HEX`, `^^hh`, `^^^^hhhh` → `Run`; lệnh có trong `CommandTable` → handler; lệnh trong `AliasTable` → thay `#n` bằng văn bản thô của đối số (`ReadRawGroup`) rồi parse lại chuỗi kết quả bằng một `LatexParser` mới; lệnh trong `SymbolTable` → `Run`; còn lại → `Error("unknown command \\foo")`. `Parse` kiểm tra không còn ký tự thừa (`}` lạc). `LatexWriter` viết `Run` theo từng ký tự (tra `SymbolTable.TryGetCommand`, thoát ký tự đặc biệt, quy tắc dấu cách ở mục Quy ước). `OmmlWriter` gộp `Run` kề nhau; `OmmlReader` bỏ `m:ctrlPr`, mọi phần tử không thuộc namespace `m`, `m:rPr` ngoài `sty`/`scr`/`nor` (Task 11 mới dùng), nút văn bản rỗng; phần tử `m:` lạ → ghi `unsupported-omml` và đọc con của nó.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 4: Phân số và nhị thức

**Files:**
- Create: `src/FormulaConvert.Core/Latex/Commands/FractionCommands.cs`, `Omml/OmmlWriter.Fractions.cs`, `Omml/OmmlReader.Fractions.cs`, `Latex/LatexWriter.Fractions.cs`
- Test: `tests/FormulaConvert.Tests/FractionTests.cs`

**Interfaces:**
- Consumes: Task 3 (`CommandTable.Add/AddInfix`, `ReadArgument`, `ReadOptionalArgument`, `ParseRowRest`, `CaseRunner`), `Frac`, `Delimited` (Task 1).
- Produces: `FractionCommands.Register(CommandTable)`; `OmmlReader`/`OmmlWriter`/`LatexWriter` xử lý `Frac` và `m:f`.

- [ ] **Step 1: Write the failing tests** (`CaseRunner.Check`; `F(n,d,t)` = `<m:f>[<m:fPr><m:type m:val="t"/></m:fPr>]<m:num>n</m:num><m:den>d</m:den></m:f>`, `D` = `m:d` mặc định ngoặc tròn):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\frac{a}{b}` | `F(R("a"),R("b"),bar)` (không `fPr`) | |
| `\dfrac{a}{b}` | như trên | `\frac{a}{b}` (+ 1 `lossy`) |
| `\tfrac{a}{b}`, `\cfrac{a}{b}`, `\cfrac[l]{1}{x+1}` | tương tự | `\frac{…}{…}` |
| `a \over b` | `F(R("a"),R("b"))` | `\frac{a}{b}` |
| `{x+1 \over 2}` | `F(R("x+1"),R("2"))` | `\frac{x+1}{2}` |
| `\binom{n}{k}` | `D(F(R("n"),R("k"),noBar))` | |
| `\dbinom{n}{k}`, `\tbinom{n}{k}` | như `\binom` | `\binom{n}{k}` |
| `n \choose k` | như `\binom` | `\binom{n}{k}` |
| `a \atop b` | `F(R("a"),R("b"),noBar)` | `{a \atop b}` |
| `\nicefrac{3}{4}` | `F(R("3"),R("4"),lin)` | |
| `\pdiff{f}{x}` | `F(R("∂f"),R("∂x"))` | `\frac{\partial f}{\partial x}` |
| `\frac{}{}` | `F(,)` (num, den rỗng) | |
| `\frac{a}{\frac{b}{c}}` | lồng | |
| `\overwithdelims`/`\atopwithdelims` | không hỗ trợ: `FormulaParseException` | |

  Reader: `m:f` có `type=skw` → đọc như `bar` kèm `lossy`; `m:d` mặc định chứa đúng một `m:f noBar` → `\binom`; `m:d` ngoặc khác chứa `m:f noBar` → `\left[ \frac…`. Writer `Frac` NoBar không bọc ngoặc ra `{a \atop b}`.
- [ ] **Step 2:** `dotnet test --filter FractionTests` → FAIL.
- [ ] **Step 3: Implement.** `\frac`-họ: hai `ReadArgument`; `\cfrac`: `ReadOptionalArgument` trước; `\over`/`\atop`/`\choose` là infix: lấy `left` (gói nhiều mục thành `FormulaRow`), gọi `ParseRowRest()` cho vế phải, xóa `left`. Hai `\over` trong cùng một hàng → `Error("ambiguous \\over")`.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 5: Căn, chỉ số và dấu phẩy trên

**Files:**
- Create: `Latex/Commands/RadicalCommands.cs`, `Latex/ScriptParsing.cs` (hậu tố `^ _ '` trong `ParseRow`), `Omml/OmmlWriter.Scripts.cs`, `Omml/OmmlReader.Scripts.cs`, `Latex/LatexWriter.Scripts.cs`
- Modify: `Latex/LatexParser.cs` (sau mỗi atom, gọi `ScriptParsing` để gắn chỉ số)
- Test: `tests/FormulaConvert.Tests/ScriptTests.cs`

**Interfaces:**
- Consumes: Task 3 parser; `Radical`, `Script`, `PreScript` (Task 1).
- Produces: `ScriptParsing.Attach(LatexParser p, FormulaNode atom): FormulaNode` (đọc liên tiếp `^`/`_`/`'` sau atom; một chỉ số trên + một chỉ số dưới tối đa; thứ tự bất kỳ). Task 6 và 9 dùng lại để gắn chỉ số cho lệnh.

- [ ] **Step 1: Write the failing tests** (`S(base,sub,sup)` = `m:sSub`/`m:sSup`/`m:sSubSup` với `<m:e>`, `<m:sub>`, `<m:sup>`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `x^2` | `sSup(e=R("x"), sup=R("2"))` | |
| `x_i` | `sSub(e=R("x"), sub=R("i"))` | |
| `x_i^2`, `x^2_i` | `sSubSup(e=x, sub=i, sup=2)` | `x_i^2` |
| `x^{ab}` | sup=`R("ab")` | |
| `ab^2` | `R("a")` + `sSup(e=R("b"), sup=R("2"))` | |
| `{ab}^2` | `sSup(e=R("ab"), sup=R("2"))` | |
| `x^{2^3}` | sup chứa `sSup` lồng | |
| `x^\alpha` | sup=`R("α")` | |
| `f'` | `sSup(e=R("f"), sup=R("′"))` | `f^{\prime}` |
| `f''` | `sSup(e=R("f"), sup=R("′′"))` | `f^{\prime\prime}` |
| `{}_a^b x` | `sPre(sub=a, sup=b, e=R("x"))` | |
| `^2` (không cơ sở) | `sSup(e=, sup=R("2"))` | |
| `\sqrt{x}` | `<m:rad><m:radPr><m:degHide m:val="1"/></m:radPr><m:deg/><m:e>R(x)</m:e></m:rad>` | |
| `\sqrt[3]{x}` | `<m:rad><m:deg>R("3")</m:deg><m:e>R(x)</m:e></m:rad>` | |
| `\sqrt{}` | `e` rỗng | |
| `\surd` | `R("√")` | |

  Lỗi: `x^1^2` và `x_1_2` → `FormulaParseException` (thông điệp chứa `double superscript`/`double subscript`, position tại dấu thứ hai). `\sqrt` thiếu đối số → exception.
- [ ] **Step 2:** `dotnet test --filter ScriptTests` → FAIL.
- [ ] **Step 3: Implement.** Atom bị gắn chỉ số là mục cuối vừa đọc (một `Run` đơn ký tự, một nhóm, hay nút từ lệnh). Hàng bắt đầu bằng `{}` rồi `_`/`^` (nhóm rỗng làm cơ sở) và sau đó là atom → `PreScript` (nhóm rỗng chỉ có chỉ số rồi một atom). Reader: `m:sSub`/`m:sSup`/`m:sSubSup`/`m:sPre`/`m:rad` đọc phần tử con theo tên.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 6: Toán tử lớn và hàm

**Files:**
- Create: `Latex/Commands/NaryCommands.cs`, `Latex/Commands/FunctionCommands.cs`, `Omml/OmmlWriter.Nary.cs`, `OmmlReader.Nary.cs`, `Latex/LatexWriter.Nary.cs`
- Test: `tests/FormulaConvert.Tests/NaryFunctionTests.cs`

**Interfaces:**
- Consumes: `NaryTable`, `FunctionTable` (Task 2), `ScriptParsing.Attach` (Task 5), `Nary`, `FunctionApply`, `Limit`, `Script`.
- Produces: `NaryCommands.Register`, `FunctionCommands.Register` (gồm `\operatorname{name}`, `\limits`, `\nolimits`, `\displaylimits`).

- [ ] **Step 1: Write the failing tests** (`N(chr,sub,sup,e,limLoc)`: `<m:nary><m:naryPr><m:chr m:val="chr"/>[<m:limLoc m:val="…"/>][<m:subHide m:val="1"/>][<m:supHide m:val="1"/>]</m:naryPr><m:sub>…</m:sub><m:sup>…</m:sup><m:e>…</m:e></m:nary>`; `limLoc` chỉ ghi khi khác mặc định của ký tự; `Fn(name,e)` = `m:func` với `m:fName` chứa `R(name, "<m:sty m:val=\"p\"/>")`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\sum_{i=0}^n a_i` | `N(∑, i=0, n, sSub(a,i))` | `\sum_{i=0}^n a_i` |
| `\int_0^1 f` | `N(∫, 0, 1, f)` (không `limLoc`) | |
| `\int_0^1\limits f` | `N(∫, …, limLoc=undOvr)` | |
| `\sum\nolimits_a^b x` | `limLoc=subSup` | |
| `\sum x` | `subHide`+`supHide`=1 | |
| `\sum_{i}^{n}{a+b}` | `e` = `R("a+b")` | `\sum_i^n{a+b}` |
| `\prod`, `\coprod`, `\oint`, `\bigcup`, `\bigoplus` | chr đúng bảng | |
| `\sin x` | `Fn(sin, R("x"))` | |
| `\sin(x+1)` | `Fn(sin, R("(x+1)"))` | |
| `\sin^2 x` | `Fn(sSup(sin,2), x)` | |
| `\lim_{x\to0} f` | `Fn(limLow(e=R(lim), lim=R("x→0")), f)` | `\lim_{x\to0}f` |
| `\operatorname{argth}(\theta)` | `Fn(argth, R("(θ)"))` | `\operatorname{argth}(\theta)` |
| `\sin` (không toán hạng) | `Fn(sin, )` (`e` rỗng) | |
| `\log_2 8` | `Fn(sSub(log, 2), 8)` (`log` không thuộc bounds) | |

  Reader: `m:nary` thiếu `m:chr` → `∫` (mặc định của Word); `m:limLoc` thiếu → mặc định theo ký tự; `m:func` với `fName` chứa nhiều run → ghép tên.
- [ ] **Step 2:** `dotnet test --filter NaryFunctionTests` → FAIL.
- [ ] **Step 3: Implement.** Handler `\sum`-họ: tạo cờ `limits` mặc định theo `NaryInfo.SubSupDefault`, đọc `\limits`/`\nolimits`/`\displaylimits` đứng ngay sau lệnh hoặc sau chỉ số, rồi `ScriptParsing` cho `_`/`^`, rồi toán hạng theo quy ước "atom kế tiếp". Hàm trong `BoundsFunctions` + chỉ số dưới → `Limit(name, sub, Upper:false)`; hàm khác → `Script`. Tên hàm là `Run` có `Sty.Plain`.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 7: Dấu ngoặc

**Files:**
- Create: `Latex/Commands/DelimiterCommands.cs`, `Omml/OmmlWriter.Delimiters.cs`, `OmmlReader.Delimiters.cs`, `Latex/LatexWriter.Delimiters.cs`
- Modify: `Latex/LatexParser.cs` (hàng kết thúc ở `\right` / `\middle` được báo cho `DelimiterCommands`)
- Test: `tests/FormulaConvert.Tests/DelimiterTests.cs`

**Interfaces:**
- Consumes: Task 3 parser, `SymbolTable`, `Delimited`.
- Produces: `DelimiterCommands.Register` (`\left`, `\right`, `\middle`, `\bigl \bigr \bigm \Bigl \Bigr \Bigm \biggl \biggr \biggm \Biggl \Biggr \Biggm` và `\big \Big \bigg \Bigg`); `LatexParser.ParseRowUntilRight()` nội bộ.

- [ ] **Step 1: Write the failing tests** (`Dl(open,close,sep,parts)` = `<m:d>[<m:dPr>[<m:begChr m:val="open"/>][<m:sepChr m:val="sep"/>][<m:endChr m:val="close"/>]</m:dPr>]<m:e>part</m:e>…</m:d>`; mặc định `(` `)` thì bỏ):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\left(x\right)` | `Dl((,),,R("x"))` (không `dPr`) | |
| `\left[x\right]` | `begChr="["` `endChr="]"` | |
| `\left\lbrace x \right\rbrace` | `{`…`}` | `\left\lbrace x\right\rbrace` |
| `\left\{ x \right\}` | như trên | `\left\lbrace x\right\rbrace` |
| `\left\langle x\right\rangle` | `⟨`…`⟩` | |
| `\left\lfloor x\right\rfloor`, `\left\lceil x\right\rceil` | `⌊⌋`, `⌈⌉` | |
| `\left| x \right|`, `\left\vert x\right\vert` | `|`…`|` | `\left| x\right|` |
| `\left\| x \right\|`, `\left\Vert x\right\Vert` | `‖`…`‖` | `\left\Vert x\right\Vert` |
| `\left( x \right.` | `endChr=""` | |
| `\left. x \right)` | `begChr=""` | |
| `\left(a\middle|b\right)` | `sepChr="|"`, hai `m:e` | |
| `\left\lparen \frac1x \right\rbrack` | `begChr` `(` `endChr` `]` | `\left(\frac{1}{x}\right]` |
| `\bigl( x \bigr)` | `R("(x)")` + 2 `lossy` (một cho mỗi lệnh cỡ) | `(x)` |
| `\left(\left[x\right]\right)` | lồng | |
| `\left(` (thiếu `\right`) | `FormulaParseException`, position = vị trí `\left` | |
| `\right)` lạc | exception | |
| `\left\foo x\right)` | exception (delimiter không hợp lệ) | |

  Reader: `m:d` nhiều `m:e` không có `sepChr` → dấu ngăn mặc định `|`; `m:d` không `dPr` → `(` `)`; `m:d` có nhiều ký tự ngăn khác nhau không thể (OMML chỉ có một `sepChr`).
- [ ] **Step 2:** `dotnet test --filter DelimiterTests` → FAIL.
- [ ] **Step 3: Implement.** Ký tự delimiter = literal (`( ) [ ] | .`), hoặc lệnh có trong `SymbolTable` (`\lbrace`, `\langle`, `\lVert`…), hoặc `\{ \} \|`; `.` = chuỗi rỗng. `\middle` nhiều lần với ký tự khác nhau → giữ ký tự đầu, ghi `lossy`.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 8: Ma trận và môi trường

**Files:**
- Create: `Latex/Commands/EnvironmentCommands.cs`, `Omml/OmmlWriter.Matrix.cs`, `OmmlReader.Matrix.cs`, `Latex/LatexWriter.Matrix.cs`
- Test: `tests/FormulaConvert.Tests/EnvironmentTests.cs`

**Interfaces:**
- Consumes: Task 3 parser (`ReadRawGroup`, `ParseRowRest`), `Matrix`, `EqArray`, `Delimited`.
- Produces: `EnvironmentCommands.Register` đăng ký `\begin` (đọc tên môi trường rồi tới `\end{tên}` tương ứng); hàng kết thúc ở `&`, `\\`, `\end`.

- [ ] **Step 1: Write the failing tests** (`Mx(rows, mcs?)`: `<m:m>[<m:mPr><m:mcs><m:mc><m:mcPr><m:count m:val="1"/><m:mcJc m:val="left|right"/></m:mcPr></m:mc>…</m:mcs></m:mPr>]<m:mr><m:e>…</m:e>…</m:mr>…</m:m>`; chỉ ghi `mcs` khi có cột không phải `c`; `EqA(rows)` = `<m:eqArr><m:e>…</m:e>…</m:eqArr>`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\begin{matrix}a&b\\c&d\end{matrix}` | `Mx([[a,b],[c,d]])` | |
| `\begin{pmatrix}a&b\\c&d\end{pmatrix}` | `Dl((,),Mx(…))` | |
| `bmatrix`, `Bmatrix`, `vmatrix`, `Vmatrix` | ngoặc `[ ]`, `{ }`, `\|`, `‖` | |
| `\begin{smallmatrix}a&b\end{smallmatrix}` | `Mx` + 1 `lossy` | `\begin{matrix}…` |
| `\begin{array}{lc}a&b\\c&d\end{array}` | `Mx` với `mcJc` = left, (cột 2 mặc định) | `\begin{array}{lc}…` |
| `\begin{array}{l\|c}…` | bỏ đường kẻ dọc + 1 `lossy` | `\begin{array}{lc}…` |
| `\begin{matrix*}[l\|r]a&b\end{matrix*}` | `mcJc` = left, right | |
| `\begin{cases}1&x>0\\0&x\le0\end{cases}` | `Dl({,,EqA([…]))` với `&` giữ trong `m:t` | |
| `\begin{rcases}…\end{rcases}` | `begChr=""`, `endChr="}"` | |
| `\begin{dcases}…` | như `cases` + 1 `lossy` | `\begin{cases}…` |
| `\begin{align}f&=a\\&=b\end{align}` | `EqA` (hai hàng, `&` nằm trong `m:t`) | |
| `aligned`, `split` (cùng nội dung có `&`) | như `align` | `\begin{align}…` |
| `\begin{gathered}a\\b\end{gathered}` | `EqA` (không `&`) | |
| `gather` | như `gathered` | `\begin{gathered}…` |
| `\begin{multline}a\\b\end{multline}` | như `gathered` + 1 `lossy` | `\begin{gathered}…` |
| `\begin{equation}x\end{equation}`, `math`, `displaymath`, `subequations`, `eqnarray`, `center` | nội dung trong suốt: `R("x")` | `x` |
| `\begin{matrix}a&b\\c\end{matrix}` | hàng thiếu ô: điền `m:e` rỗng đến đủ số cột | |
| `\begin{matrix}a\\\end{matrix}` | hàng cuối rỗng sau `\\` bị bỏ | |
| `\begin{matrix}a&b\end{pmatrix}` | `FormulaParseException` (chứa tên mong đợi và tên gặp) | |
| `\begin{matrix}a&b` (thiếu `\end`) | exception | |
| `\begin{foo}x\end{foo}` | exception | |

- [ ] **Step 2:** `dotnet test --filter EnvironmentTests` → FAIL.
- [ ] **Step 3: Implement.** Reader: `m:m` đứng một mình → `matrix`; `m:d` có đúng một con `m:m` và ngoặc khớp bảng (`()`→`pmatrix`, `[]`→`bmatrix`, `{}`→`Bmatrix`, `||`→`vmatrix`, `‖‖`→`Vmatrix`) → môi trường tương ứng, ngoặc khác → `\left…\begin{matrix}…\right…`; `m:d` `{` + `m:eqArr` → `cases`; `m:d` `""`+`}` + `m:eqArr` → `rcases`; `m:eqArr` trần → `align` nếu có `&` trong `m:t`, ngược lại `gathered`.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 9: Dấu phụ, gạch, ngoặc trên/dưới, đặt trên/dưới

**Files:**
- Create: `Latex/Commands/AccentCommands.cs`, `Latex/Commands/OverUnderCommands.cs`, `Omml/OmmlWriter.Accents.cs`, `OmmlReader.Accents.cs`, `Latex/LatexWriter.Accents.cs`
- Test: `tests/FormulaConvert.Tests/AccentTests.cs`

**Interfaces:**
- Consumes: `AccentTable`, `SymbolTable`, `Accent`, `Bar`, `GroupChar`, `Limit`.
- Produces: `AccentCommands.Register`, `OverUnderCommands.Register`.

- [ ] **Step 1: Write the failing tests** (`Ac(chr,e)` = `<m:acc><m:accPr><m:chr m:val="chr"/></m:accPr><m:e>e</m:e></m:acc>`; `Br(top?,e)` = `<m:bar>[<m:barPr><m:pos m:val="bot"/></m:barPr>]<m:e>…</m:e></m:bar>` (top là mặc định); `Gc(chr,top,e)` = `<m:groupChr><m:groupChrPr><m:chr m:val="chr"/><m:pos m:val="top|bot"/></m:groupChrPr><m:e>…</m:e></m:groupChr>`; `LU(e,lim)`=`m:limUpp`, `LL(e,lim)`=`m:limLow`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\hat{x}`, `\tilde{x}`, `\vec{v}`, `\dot{x}`, `\ddot{x}`, `\bar{x}`, `\check{x}`, `\breve{x}`, `\mathring{x}` | `Ac(chr từ accents.csv, …)` | |
| `\dddot{x}`, `\ddddot{x}` | `Ac(U+20DB / U+20DC)` | |
| `\acute{e}`, `\grave{e}` | `Ac(U+0301 / U+0300)` | |
| `\widehat{ABC}`, `\widetilde{ABC}`, `\widecheck{ABC}` | như `\hat` | `\hat{ABC}`, `\tilde{ABC}`, `\check{ABC}` |
| `\overrightarrow{AB}`, `\overleftarrow{AB}`, `\overleftrightarrow{AB}` | `Ac(U+20D7 / U+20D6 / U+20E1)` | `\vec{AB}` cho phải; hai cái còn lại giữ tên |
| `\overline{AB}` | `Br(top, AB)` | |
| `\underline{AB}` | `Br(bot, AB)` | |
| `\overbrace{AB}` | `Gc(⏞, top, AB)` | |
| `\underbrace{AB}` | `Gc(⏟, bot, AB)` | |
| `\overparen{AB}`, `\wideparen{AB}` | `Gc(⏜, top)` | `\overparen{AB}` |
| `\underparen{AB}` | `Gc(⏝, bot)` | |
| `\underrightarrow{AB}` | `Gc(→, bot)` | |
| `\underleftarrow{AB}` | `Gc(←, bot)` | |
| `\underleftrightarrow{AB}` | `Gc(↔, bot)` | |
| `\overset{a}{x}`, `\stackrel{a}{x}` | `LU(x, a)` | `\overset{a}{x}` |
| `\underset{b}{x}` | `LL(x, b)` | |
| `\overunderset{a}{b}{x}` | `LU(LL(x,b), a)` | |
| `\xrightarrow{a}` | `LU(R("→"), a)` | |
| `\xrightarrow[b]{a}` | `LU(LL(R("→"), b), a)` | |
| `\xleftarrow[b]{}` | `LL(R("←"), b)` (đối số trên rỗng bị bỏ) | `\xleftarrow[b]{}` |
| `\xlongequal{a}`, `\xRightarrow{a}`, `\xmapsto{a}` | cơ sở `=`, `⇒`, `↦` | |
| `\hat` thiếu đối số | `FormulaParseException` | |

  Quy tắc chung cho mọi `\x…`: ký tự mũi tên = codepoint của lệnh bỏ chữ `x` đầu (`\xrightarrow` → `\rightarrow`); ngoại lệ `\xlongequal` = `=`, `\xtofrom` = `\rightleftarrows`, `\xmapsto` = `\mapsto`. Writer: `Limit` có cơ sở là một ký tự mũi tên thuộc tập này (có thể lồng hai tầng) → `\x…[dưới]{trên}`, ngược lại → `\overset`/`\underset`/`\overunderset`. Các lệnh trong mục Extensible Accents không có ánh xạ trên (`\overgroup \undergroup \overlinesegment \underlinesegment \overarc \underarc \overleftharpoon \overrightharpoon \Overrightarrow \utilde`) sẽ được đưa vào `outofscope.csv` ở Task 12 (giữ nội dung, bỏ lệnh).
- [ ] **Step 2:** `dotnet test --filter AccentTests` → FAIL.
- [ ] **Step 3: Implement** theo bảng; `\hat`-họ đọc một đối số.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 10: Khung viền và phantom

**Files:**
- Create: `Latex/Commands/BoxCommands.cs`, `Omml/OmmlWriter.Boxes.cs`, `OmmlReader.Boxes.cs`, `Latex/LatexWriter.Boxes.cs`
- Test: `tests/FormulaConvert.Tests/BoxTests.cs`

**Interfaces:**
- Consumes: `BorderBox`, `BoxEdges`, `Phantom` (Task 1), `ReadRawGroup`, `ReadOptionalArgument`.
- Produces: `BoxCommands.Register` (`\boxed`, `\cancel`, `\bcancel`, `\xcancel`, `\enclose`, `\phantom`, `\hphantom`, `\vphantom`, `\smash`).

- [ ] **Step 1: Write the failing tests** (`Bb(flags,e)` = `<m:borderBox>[<m:borderBoxPr>…</m:borderBoxPr>]<m:e>…</m:e></m:borderBox>`; các cờ theo thứ tự `hideTop, hideBot, hideLeft, hideRight, strikeH, strikeV, strikeBLTR, strikeTLBR`; `hideX` ghi `1` cho cạnh **không** có trong `Edges`; `Ph(…)` = `<m:phant><m:phantPr>[show=0][zeroWid=1][zeroAsc=1][zeroDesc=1]</m:phantPr><m:e>…</m:e></m:phant>`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\boxed{x}` | `Bb(không cờ)` | |
| `\cancel{x}` | 4 `hide*`=1 + `strikeBLTR`=1 | |
| `\bcancel{x}` | 4 `hide*`=1 + `strikeTLBR`=1 | |
| `\xcancel{x}` | 4 `hide*` + `strikeBLTR` + `strikeTLBR` | |
| `\enclose{box}{x}` | không cờ | `\boxed{x}` |
| `\enclose{top bottom}{x}` | `hideLeft`=1, `hideRight`=1 | `\enclose{top bottom}{x}` |
| `\enclose{horizontalstrike}{x}`, `verticalstrike`, `updiagonalstrike`, `downdiagonalstrike` | 4 `hide*` + cờ tương ứng | `\enclose{horizontalstrike}{x}`…; `updiagonalstrike`→`\cancel`, `downdiagonalstrike`→`\bcancel` |
| `\enclose{roundedbox}{x}` | như `box` + 1 `lossy` | `\boxed{x}` |
| `\enclose{circle}[mathcolor="red"]{x}` | `R("x")` + `out-of-scope` (circle và style) | `x` |
| `\phantom{x}` | `Ph(show=0)` | |
| `\hphantom{x}` | `Ph(show=0, zeroAsc, zeroDesc)` | |
| `\vphantom{x}` | `Ph(show=0, zeroWid)` | |
| `\smash{x}` | `Ph(zeroAsc, zeroDesc)` (show mặc định) | |
| `\smash[b]{x}` | như `\smash` (tham số tùy chọn bị bỏ) | `\smash{x}` |
| `\boxed` thiếu đối số | `FormulaParseException` | |

- [ ] **Step 2:** `dotnet test --filter BoxTests` → FAIL.
- [ ] **Step 3: Implement.** `\enclose{…}`: tách ký hiệu theo khoảng trắng; ký hiệu không có ánh xạ (`circle`, `updiagonalarrow`, `phasorangle`, `radical`, `longdiv`, `actuarial`, `madruwb`) bị bỏ kèm `out-of-scope`; nếu `Edges` rỗng → trả thân; đối số `[style]` luôn bỏ kèm `out-of-scope`. Writer: `BoxEdges` đúng bốn cạnh → `\boxed`; đúng `StrikeUp` → `\cancel`; đúng `StrikeDown` → `\bcancel`; `StrikeUp|StrikeDown` → `\xcancel`; còn lại → `\enclose{…}` liệt kê theo thứ tự `box top bottom left right horizontalstrike verticalstrike updiagonalstrike downdiagonalstrike` (`box` thay cho bốn cạnh đủ).
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 11: Kiểu chữ và văn bản

**Files:**
- Create: `Latex/Commands/StyleCommands.cs`, `Omml/OmmlWriter.Styles.cs`, `OmmlReader.Styles.cs`, `Latex/LatexWriter.Styles.cs`
- Modify: `Omml/OmmlReader.cs` (đọc `m:rPr`: `m:sty`, `m:scr`, `m:nor`)
- Test: `tests/FormulaConvert.Tests/StyleTests.cs`; Modify: `CoreTests.Reader_ignores_word_noise` (assert mới `\mathrm{x}`)

**Interfaces:**
- Consumes: `Run`, `RunStyle`, `Sty`, `Scr` (Task 1); `ReadArgument`, `ReadRawGroup`.
- Produces: `StyleCommands.Register`; `StyleCommands.Apply(FormulaNode body, Func<RunStyle,RunStyle> change)` đổi kiểu mọi `Run` bên trong `body`.

- [ ] **Step 1: Write the failing tests** (`R(text, rPr)` từ `Xml`):

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\mathbf{x}` | `R("x", "<m:sty m:val=\"b\"/>")` | |
| `\boldsymbol{x}`, `\bm{x}`, `\bold{x}`, `\textbf{x}` | như trên | `\mathbf{x}` |
| `\mathrm{x}` | `sty="p"` | |
| `\mathit{x}`, `\textit{x}` | `R("x")` (kiểu mặc định) | `x` |
| `\mathbfit{x}` | `sty="bi"` | |
| `\mathbb{R}` | `R("R", "<m:scr m:val=\"double-struck\"/>")` | |
| `\Bbb{R}` | như trên | `\mathbb{R}` |
| `\mathcal{A}`, `\mathscr{A}` | `scr="script"` | `\mathcal{A}` |
| `\mathfrak{g}` | `scr="fraktur"` | |
| `\mathsf{x}` | `scr="sans-serif"` | |
| `\mathtt{x}`, `\texttt` | `scr="monospace"` | `\mathtt{x}` |
| `\mathbf{\mathbb{R}}` | `sty="b"` và `scr="double-struck"` | `\mathbf{\mathbb{R}}` |
| `\mathbf{x+1}` | một run `x+1` | |
| `\mathbf{\frac{a}{b}}` | kiểu áp dụng cho `Run` bên trong phân số | |
| `\text{if } x` | `R("if ", "<m:nor/>")` + `R("x")` | `\text{if }x` |
| `\text{a b}`, `\textrm{…}`, `\textnormal{…}`, `\mbox{…}` | `<m:nor/>`, giữ khoảng trắng, `xml:space="preserve"` trên `m:t` | `\text{a b}` |
| `\text{50\%}` | `R("50%", nor)` | `\text{50\%}` |
| `\text{unclosed` | `FormulaParseException` | |

  Reader: `m:r` có cả `sty` và `scr` hợp lệ; `sty` ngoài `p b i bi` → bỏ qua kèm `unsupported-omml`; `m:nor` thắng mọi kiểu khác. Đầu vào Word có `w:rPr` kèm `m:rPr` trong cùng `m:r` vẫn đọc đúng.
- [ ] **Step 2:** `dotnet test --filter StyleTests` → FAIL.
- [ ] **Step 3: Implement.** `\text`-họ lấy văn bản thô (`ReadRawGroup`, hiểu `\%` v.v. và lồng `{}`), tạo một `Run` `Normal=true`. Writer: nhóm `Run` liên tiếp cùng kiểu → một lệnh kiểu bọc ngoài; kết hợp `Sty` bọc ngoài `Scr`.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 12: Lệnh ngoài phạm vi

**Files:**
- Create: `Latex/Commands/OutOfScopeCommands.cs`
- Modify: `src/FormulaConvert.Core/Data/outofscope.csv`
- Test: `tests/FormulaConvert.Tests/OutOfScopeTests.cs`

**Interfaces:**
- Consumes: `OutOfScopeTable` (Task 2), `ReadArgument`, `ReadRawGroup`, `Diagnostic`.
- Produces: `OutOfScopeCommands.Register(CommandTable)` đăng ký mọi lệnh trong `outofscope.csv`; handler đọc `Arity` đối số, giữ đối số thứ `KeptArg` (đếm từ 0), thêm một `Diagnostic("out-of-scope", "\\name is not supported by OMML", position)`; `KeptArg = -1` trả `FormulaRow.Empty`.

- [ ] **Step 1: Write the failing tests** (kiểm tra OMML và đúng một `Diagnostic` mã `out-of-scope` với `Position` = vị trí lệnh):

| Latex | Output OMML | Số cảnh báo |
|---|---|---|
| `\textcolor{red}{x+1}` | `R("x+1")` | 1 |
| `\colorbox{yellow}{x}`, `\fcolorbox{a}{b}{x}` | `R("x")` | 1 |
| `{\color{blue} x+1}` | `R("x+1")` | 1 |
| `\Large x`, `\tiny{x}` | `R("x")` | 1 |
| `\href{u}{x}`, `\class{c}{x}`, `\cssId{i}{x}`, `\htmlData{k=v}{x}` | `R("x")` | 1 |
| `\texttip{x}{tip}`, `\mathtip{x}{y}` | `R("x")` | 1 |
| `\error{x}` | `R("x")` | 1 |
| `\ce{H2O}` | `R("H2O", nor)` | 1 |
| `a\quad b`, `a\hspace{1em}b`, `a\,b`, `a\;b`, `a\!b`, `a\qquad b`, `a\enskip b`, `a~b` | `R("ab")` | 1 mỗi lệnh |
| `\displaystyle x`, `\textstyle x`, `\scriptstyle x` | `R("x")` | 1 |
| `\mathop{x}`, `\mathrel{x}`, `\mathbin{x}`, `\mathord{x}`, `\mathopen{x}`, `\mathclose{x}`, `\mathpunct{x}`, `\mathinner{x}` | `R("x")` | 1 |
| `\overgroup{AB}`, `\underlinesegment{AB}`, `\utilde{AB}` | `R("AB")` | 1 |
| `\relax`, `\limits` lạc | bị bỏ | 1 |
| `\textcolor{red}` (thiếu đối số) | `FormulaParseException` | |

- [ ] **Step 2:** `dotnet test --filter OutOfScopeTests` → FAIL.
- [ ] **Step 3: Implement.** Điền `outofscope.csv` đủ các lệnh trên cùng họ của chúng trong tài liệu (mọi `\color…`, kích thước `\tiny … \Huge`, kiểu `\displaystyle …`, khoảng trắng, họ font `\fontfamily \fontseries \fontshape \selectfont \upshape \slshape \itshape \bfseries \mdseries \ttfamily \sffamily \rmfamily \scshape \textsc \textsf \textsl \textup \textmd \em \emph \bf \it \frak`, cùng các lệnh bố cục như `\rlap \llap \mathllap \mathrlap \rule \bbox \raisebox \fbox`). Lệnh dạng khai báo (`\color`, `\Large`, `\bfseries`…) có `Arity` 0 hoặc 1 tùy dạng dùng; nếu theo sau là `{…}` thì giữ nhóm đó.
- [ ] **Step 4:** `dotnet test` → PASS.

---

## Task 13: Giao diện dòng lệnh

**Files:**
- Create: `src/FormulaConvert.Cli/Program.cs`
- Test: `tests/FormulaConvert.Tests/CliTests.cs` (tham chiếu thêm project Cli)

**Interfaces:**
- Consumes: `FormulaConverter` (Task 3).
- Produces: `public static class CliApp { public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr); }` và `Program.Main` gọi nó với `Console.In/Out/Error`.

- [ ] **Step 1: Write the failing tests:**
  - `Latex2omml_reads_stdin`: `Run(["latex2omml"], "\frac{a}{b}", …)` → mã `0`, stdout chứa `<m:f>`, kết thúc bằng một xuống dòng, stderr rỗng.
  - `Omml2latex_reads_file`: ghi file tạm chứa OMML, `Run(["omml2latex", path], …)` → `0`, stdout = `\frac{a}{b}\n`.
  - `Warnings_go_to_stderr`: `\textcolor{red}{x}` → `0`, stderr chứa `warning [out-of-scope]` và vị trí, stdout là OMML.
  - `Parse_error_exits_1`: `\frac{a` → `1`, stdout rỗng, stderr bắt đầu bằng `error:` và có `position`.
  - `Missing_file_exits_1`, `Unknown_subcommand_exits_1` (stderr in cách dùng), `No_args_exits_1`.
  - `Malformed_xml_exits_1`.
- [ ] **Step 2:** `dotnet test --filter CliTests` → FAIL.
- [ ] **Step 3: Implement** `CliApp.Run`: định dạng cảnh báo `warning [code] position N: message`, lỗi `error: message (position N)`; đọc file bằng UTF-8; không in gì ra stdout khi lỗi.
- [ ] **Step 4:** `dotnet test` → PASS, rồi chạy thật: `echo '\frac{a}{b}' | dotnet run --project src/FormulaConvert.Cli -- latex2omml` in `<m:oMath …><m:f>…`, và đưa kết quả đó vào `omml2latex` ra `\frac{a}{b}`.

---

## Task 14: Kiểm tra độ phủ catalog và quét toàn bộ

**Files:**
- Create: `src/FormulaConvert.Core/Latex/CommandClassifier.cs`
- Test: `tests/FormulaConvert.Tests/CatalogTests.cs`

**Interfaces:**
- Consumes: mọi bảng Task 2, `CommandTable.Default` (Task 3–12).
- Produces: `internal static class CommandClassifier { static IReadOnlyList<string> Categories(string command); }` trả các nhóm chứa lệnh trong `{Symbol, Function, Nary, Accent, Alias, OutOfScope, Structural}`; `Structural` = `CommandTable.Default.Has(command)`.

- [ ] **Step 1: Write the failing tests:**
  - `Every_documented_command_is_classified`: với từng dòng của `mathlive-commands.txt`, `Categories(c)` không rỗng; thông báo lỗi liệt kê toàn bộ lệnh chưa phân loại. (Spec yêu cầu "đúng một nhóm"; với lệnh thuộc nhiều bảng, đường parse ưu tiên theo thứ tự Structural → OutOfScope → Alias → Symbol, nên test chỉ đòi **ít nhất một** nhóm.)
  - `Every_alias_parses`: với mỗi alias trong `aliases.csv`, `LatexToOmml` của lệnh (kèm `{x}` cho mỗi đối số) không ném lỗi.
  - `Every_structural_spec_command_is_registered`: `CommandTable.Default.Has` đúng với `\frac \dfrac \tfrac \cfrac \binom \dbinom \tbinom \over \atop \choose \pdiff \nicefrac \sqrt \left \right \middle \begin \end \hat \vec \overline \underline \overbrace \underbrace \overset \underset \overunderset \stackrel \xrightarrow \boxed \cancel \bcancel \xcancel \enclose \phantom \hphantom \vphantom \smash \mathbf \mathit \mathbfit \mathrm \mathbb \mathcal \mathscr \mathfrak \mathsf \mathtt \text \textrm \operatorname \sum \int \prod \limits \nolimits`.
  - `Every_environment_in_spec_is_accepted`: `array matrix pmatrix bmatrix Bmatrix vmatrix Vmatrix smallmatrix cases dcases rcases align aligned gather gathered split multline equation math displaymath` đều parse được với nội dung `a`.
- [ ] **Step 2:** `dotnet test --filter CatalogTests` → FAIL ở các lệnh chưa phân loại.
- [ ] **Step 3: Implement `CommandClassifier`**, rồi xử lý mọi lệnh test báo thiếu: thêm vào bảng thích hợp (`aliases.csv`, `outofscope.csv`, `symbols.csv`) hoặc đăng ký handler; không xóa lệnh khỏi `mathlive-commands.txt`.
- [ ] **Step 4:** `dotnet test` → toàn bộ PASS; in số lệnh theo từng nhóm ra output test để làm báo cáo độ phủ.
