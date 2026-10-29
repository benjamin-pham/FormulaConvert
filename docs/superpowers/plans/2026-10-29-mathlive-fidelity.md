# MathLive Fidelity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** LaTeX do `OmmlToLatex` sinh ra hiển thị đúng trong MathLive 0.111.1, LaTeX do MathLive sinh ra đọc được trọn vẹn, và OMML sinh ra khớp cách Word đánh dấu (khoảng trắng, điểm căn hàng, phân số xiên).

**Architecture:** Danh sách lệnh MathLive trích từ bundle npm làm dữ liệu test. Bảng `not-in-mathlive.txt` làm dữ liệu runtime, để chặn xuất lệnh MathLive không có. Các sửa còn lại nằm trong từng lát cắt có sẵn (`Latex/Commands/*`, `LatexWriter.*`, `OmmlReader.*`, `OmmlWriter.*`) và bảng CSV trong `src/FormulaConvert.Core/Data/`.

**Tech Stack:** C#, .NET 10, `System.Xml.Linq`, xUnit; Python 3 cho công cụ trích dữ liệu (chỉ đọc văn bản).

**Spec:** `docs/superpowers/specs/2026-10-29-mathlive-fidelity-design.md`

## Global Constraints

- Chỉ `FormulaConverter`, `ConvertResult`, `Diagnostic`, `FormulaParseException` là `public`; AST giữ `internal` (`PublicSurfaceTests`).
- TDD: test đỏ trước, rồi sửa. Test chuyển đổi dùng `CaseRunner.Check` (LaTeX→OMML, OMML→LaTeX chuẩn, parse lại). Ca nào cố ý không khứ hồi được thì dùng assert riêng và ghi lý do trong tên test.
- Thêm hoặc đổi lệnh LaTeX thì cập nhật bảng dữ liệu và `CatalogTests` (mỗi lệnh thuộc đúng một nhóm).
- Một lệnh chỉ được đăng ký một lần: `CommandTable.Add` và `OutOfScopeCommands.Register` ném khi trùng. Chuyển lệnh sang nhóm khác thì phải xóa dòng cũ.
- OMML tối giản, đúng thứ tự phần tử ECMA-376; không bao giờ ghi `m:m`, `m:mr`, `m:eqArr` rỗng.
- Đầu vào không tin cậy chỉ được ném `FormulaParseException`; giữ `NestingGuard`, cấm DTD, từ chối code point XML không hợp lệ.
- Nguồn MathLive: `package/mathlive.mjs` trong `https://registry.npmjs.org/mathlive/-/mathlive-0.111.1.tgz`, tải vào một thư mục rỗng mới. Công cụ chỉ đọc file như văn bản, chạy bằng `python3 -I`.
- Task nào sửa test cũ thì nêu tên test đó trong báo cáo, kèm lý do theo spec.
- Đổi quy ước của writer thì cập nhật mục "Quy ước" trong `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` ngay trong task đó.
- `dotnet test` xanh trước mỗi commit; commit trên `main`, tiền tố `feat:`/`fix:`/`chore:`/`docs:`.

## Review Focus

1. Ký tự khoảng trắng Unicode và ký tự trong `not-in-mathlive.txt` nằm trong `\text{…}` (`m:nor`) phải giữ nguyên văn, không thành `\,` hay `\mathrel{…}` (Task 1, Task 3).
2. `m:aln` ngoài `m:eqArr` (trong ô `m:m`, ngay dưới `m:oMath`, trong cấu trúc lồng như `m:f` bên trong hàng) bị bỏ qua, không sinh `&` lạc (Task 7).
3. Phân số `lin` làm cơ số chỉ số, toán hạng hoặc tử số (`{a/b}^2`, `\sum{a/b}`, `\frac{a/b}{c}`) phải xuất LaTeX parse lại được và giữ cấu trúc (Task 4).
4. Hàng `align` có `&` ở cuối hoặc hai `&` liền nhau (`x&\\y&`, `a&&b`) phải khứ hồi qua `m:aln` với `m:t` rỗng (Task 7).
5. Khoảng trắng trong `\operatorname{arg\,max}` vẫn bị bỏ, không sinh ký tự U+2009 trong tên hàm (Task 3).

---

### Task 1: Danh sách lệnh MathLive và chặn xuất lệnh MathLive không có

**Files:**
- Create:
  - `tools/extract_mathlive_defined.py`;
  - `tests/FormulaConvert.Tests/Data/mathlive-defined.txt` (sinh ra, `CopyToOutputDirectory`);
  - `tests/FormulaConvert.Tests/Harness/MathLiveCatalog.cs`;
  - `src/FormulaConvert.Core/Data/not-in-mathlive.txt` (EmbeddedResource như các bảng khác);
  - `src/FormulaConvert.Core/Data/NotInMathLive.cs`.
- Modify: `Data/SymbolTable.cs`, `Latex/LatexWriter.cs` (`WriteChar`), `Latex/LatexWriter.Nary.cs` (`WriteFunctionName`, n-ary), `Latex/Commands/OutOfScopeCommands.cs` (`Handle`), `THIRD-PARTY-NOTICES.md`, `AGENTS.md`.
- Test: `tests/FormulaConvert.Tests/MathLiveOutputTests.cs`; sửa `OutOfScopeTests.cs`.

**Interfaces:**
- Produces:
  - `internal static class NotInMathLive { bool TryGetKind(string command, out string kind); }`, với `kind` ∈ `rel bin ord fn nary`.
  - `SymbolTable.TryGetCommand(cp)` bỏ qua lệnh có trong `NotInMathLive`; thêm `SymbolTable.TryGetParseOnlyKind(int cp, out string kind)`.
  - `MathLiveCatalog.Defined` (`IReadOnlySet<string>` lệnh có `\`) và `MathLiveCatalog.Environments` (`IReadOnlySet<string>`).

- [ ] **Step 1: Sinh danh sách.** Viết `tools/extract_mathlive_defined.py <mathlive.mjs> <out>`. Công cụ trích:
  - tên trong `defineFunction(...)`, `defineSymbol(s)`, mảng `["\\x", …]`;
  - khóa của `DEFAULT_MACROS`, các bảng macro con và `ACCENTS`;
  - môi trường trong `defineEnvironment`/`defineTabularEnvironment`/`defineRootEnvironment`, ghi thành dòng `env:<tên>`.

  Yêu cầu:
  - nhận cả nháy đơn lẫn nháy kép, vì bundle dùng nháy kép;
  - bỏ qua khối `var INLINE_SHORTCUTS = {…}`, khối `mhchem`, và khóa không phải tên lệnh (`def`, `args`, `expand`, `captureSelection`…).

  Ghi nguồn bundle vào `THIRD-PARTY-NOTICES.md`. Thêm vào AGENTS.md (mục Rules) quy tắc: "`LatexWriter` chỉ xuất lệnh có trong MathLive; lệnh chỉ để đọc thì liệt kê trong `not-in-mathlive.txt` (`MathLiveOutputTests`)". Sửa luôn các tên cũ trong AGENTS.md: `MathConvert`→`FormulaConvert`, `MathConverter`→`FormulaConverter`, `MathParseException`→`FormulaParseException`.
- [ ] **Step 2: Viết test đỏ** trong `MathLiveOutputTests`:
  - `Catalog_has_canaries`: `\frac \nicefrac \left \right \middle \hat \vec \tilde \check \deg \bmod \pmod \mathscr \overrightarrow \widehat \mathrlap \mathrel \quad \> \enskip`, cùng `env:aligned env:gathered env:Vmatrix env:rcases`, đều có trong danh sách; `\sech` và `\csch` không có. Thiếu thì sửa regex của công cụ, không thêm tay.
  - `Every_emitted_command_is_defined_in_mathlive`:
    - Với mọi codepoint trong `symbols.csv`, `nary.csv`, `accents.csv` và mọi tên trong `functions.txt`, dựng nút tương ứng: `Run`, `Nary`, `Accent`, `FunctionApply(Run(tên không có \, Plain), Run("x"))`.
    - Gọi `LatexWriter.Write`, tách mọi `\\[A-Za-z]+`, `\\.` và `\begin{…}`.
    - Mỗi lệnh phải có trong `Defined`, mỗi môi trường trong `Environments`.
    - Thông báo lỗi liệt kê toàn bộ vi phạm.
  - `Parse_only_symbols_still_parse`: `\nequiv` → `R("≢")`.
  - `Parse_only_symbols_are_written_by_kind` (assert riêng, không khứ hồi):

    | OMML | LaTeX |
    |---|---|
    | `R("a≢b")` | `a\mathrel{≢}b` |
    | `R("∟")` | `∟` |
    | `R("¶")` | `¶` |
    | `func` có `fName` là `sech` | `\operatorname{sech}x` |

    `LatexToOmml("a\\mathrel{≢}b")` cho lại `R("a≢b")` và không có diagnostic.
  - `Mathrel_with_several_characters_still_warns`: `\mathrel{ab}` → một `out-of-scope`.
  - `Parse_only_symbol_in_text_is_literal` (Review Focus 1): `Check("\\text{≢}", R("≢", Nor))`.
- [ ] **Step 3: Chạy** `dotnet test --filter MathLiveOutputTests`. Kết quả mong đợi: FAIL. Test thứ hai liệt kê vi phạm, dự kiến `\iddots \nequiv \nsimeq \napprox \nasymp \ntlg \ntgl \nsqsubseteq \nsqsupseteq \rightangle \measuredrightangle \varlrtriangle \epar \qed \Euler \dotminus \dlsh \drsh \leftwavearrow \rightwavearrow \P \sech \csch`.
- [ ] **Step 4: Cài đặt.**
  - Điền `not-in-mathlive.txt` theo danh sách vi phạm thực tế:

    | `kind` | Lệnh |
    |---|---|
    | `rel` | quan hệ, mũi tên, `\epar` |
    | `bin` | `\dotminus` |
    | `ord` | `\iddots \rightangle \measuredrightangle \varlrtriangle \qed \Euler \P` |
    | `fn` | `\sech \csch` |
    | `nary` | toán tử lớn |

  - `WriteChar` dùng `TryGetParseOnlyKind`: `rel` thì `\mathrel{c}`, `bin` thì `\mathbin{c}`, còn lại ghi ký tự.
  - `WriteFunctionName`: tên có `kind` là `fn` ghi `\operatorname{…}`.
  - `OutOfScopeCommands.Handle`: chỉ với `\mathrel` và `\mathbin`, khi đối số giữ lại là một `Run` có đúng một `Rune`, thì không cảnh báo.
  - Sửa các dòng `\mathrel{x}` và `\mathbin{x}` trong `OutOfScopeTests` (khoảng dòng 40-46) sang đối số hai ký tự (`{xy}`) để vẫn kiểm cảnh báo.
- [ ] **Step 5: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa các test cũ đang chờ `\nequiv`, `\qed`… sang dạng mới.
- [ ] **Step 6: Commit** `fix: never write LaTeX commands MathLive does not define`.

### Task 2: Lệnh chuẩn khi đọc ngược ký hiệu

**Files:**
- Modify: `src/FormulaConvert.Core/Data/symbols.csv` (thứ tự dòng), `tools/extract_symbols.py` (`PREFERRED`)
- Test: `tests/FormulaConvert.Tests/DataTableTests.cs`

**Interfaces:**
- Consumes: `SymbolTable.TryGetCommand` (Task 1).

- [ ] **Step 1: Viết test đỏ** `Canonical_reverse_command` (Theory trên `SymbolTable.TryGetCommand(cp)`) với 13 cặp:
  - `0x2270 \nleq`, `0x2271 \ngeq`, `0x2288 \nsubseteq`, `0x2289 \nsupseteq`;
  - `0x2224 \nmid`, `0x2226 \nparallel`, `0x2A87 \lneq`, `0x2A88 \gneq`;
  - `0x2248 \approx`, `0x223C \sim`, `0x2225 \parallel`, `0x2223 \mid`, `0x27F7 \longleftrightarrow`.
- [ ] **Step 2: Chạy** `dotnet test --filter Canonical_reverse_command`. Kết quả mong đợi: cả 13 ca FAIL.
- [ ] **Step 3: Cài đặt.** Đưa dòng chuẩn lên trước trong `symbols.csv`; thêm 13 cặp vào `PREFERRED`.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật kỳ vọng cũ (`\thickapprox`, `\thicksim`, `\Vert` cho U+2225, `\vert` cho U+2223, `\biconditional`) và ví dụ ký hiệu chuẩn trong "Quy ước". Bộ thoát ngoặc của `Delimited` (`‖` U+2016 → `\Vert`) không đổi.
- [ ] **Step 5: Commit** `fix: write the common LaTeX command for shared symbol codepoints`.

### Task 3: Khoảng trắng hai chiều

**Files:**
- Create: `src/FormulaConvert.Core/Latex/Commands/SpaceCommands.cs`, `src/FormulaConvert.Core/Data/SpaceTable.cs`
- Modify:
  - `Data/outofscope.csv`: xóa `\quad \qquad \enskip \enspace \space \; \: \! \ `;
  - `Latex/Commands/OutOfScopeCommands.cs`: xóa dòng tự đăng ký `\,`;
  - `Latex/LatexParser.cs`: `~` thành U+00A0, thay cho cảnh báo;
  - `Latex/Commands/FunctionCommands.cs`: bỏ ký tự khoảng trắng khỏi tên `\operatorname{…}`;
  - `Latex/LatexWriter.Styles.cs` (`WriteStyledRuns`);
  - `Latex/CommandClassifier.cs`: nhóm `Space`.
- Test: `tests/FormulaConvert.Tests/SpaceTests.cs`; sửa `OutOfScopeTests.cs` (các dòng khoảng trắng) và `CatalogTests.cs` (thêm `"Space"` vào `CategoryNames`).

**Interfaces:**
- Produces:
  - `internal static class SpaceTable { bool TryGetText(string command, out string text); bool TryGetCommand(int cp, out string command); bool IsSpace(int cp); }`;
  - `LatexWriter.WriteMathText(string text, LatexBuilder b)`: thay vòng lặp `WriteChar` trong `WriteStyledRuns`, gộp hai U+2003 thành `\qquad`.

- [ ] **Step 1: Viết test đỏ** (`CaseRunner.Check`, OMML là một run):

| Latex | `m:t` | Canonical |
|---|---|---|
| `a\,b` | `a\u2009b` | |
| `a\:b` | `a\u2005b` | |
| `a\>b` | `a\u2005b` | `a\:b` |
| `a\;b` | `a\u2004b` | |
| `a\quad b` | `a\u2003b` | |
| `a\qquad b` | `a\u2003\u2003b` | |
| `a\enspace b` | `a\u2002b` | |
| `a\enskip b` | `a\u2002b` | `a\enspace b` |
| `a\ b` | `a\u00A0b` | |
| `a~b`, `a\space b`, `a\nobreakspace b` | `a\u00A0b` | `a\ b` |

  Thêm các test:
  - OMML `a\u2006b`, `a\u200Ab` → `a\,b`; `a\u205Fb` → `a\:b`; `a b` (U+0020) → `a\ b`.
  - `a\!b` → `R("ab")`, một `lossy`.
  - `Check("\\text{a\u2009b}", R("a\u2009b", Nor))` (Review Focus 1).
  - `Check("\\quad x", …)` → `m:t` có `xml:space="preserve"`, vì quy tắc hiện có xét ký tự đầu và cuối.
  - `\int f(x)\,dx` → không còn diagnostic.
  - `\operatorname{arg\,max}x` → tên hàm `argmax`, không có U+2009 (Review Focus 5). `NaryFunctionTests.Operatorname_argument_spacing_is_dropped` phải tiếp tục xanh.
- [ ] **Step 2: Chạy** `dotnet test --filter SpaceTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D3 trong spec. Quy tắc `xml:space` trong `OmmlWriter.WriteRun` không đổi. Sau `\quad` và `\enspace`, `LatexBuilder.Command` tự chèn dấu cách khi gặp chữ cái; sau `\,`, `\:`, `\;`, `\ ` thì không cần.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Thêm dòng khoảng trắng vào "Quy ước".
- [ ] **Step 5: Commit** `feat: map LaTeX spacing commands to Unicode spaces in OMML`.

### Task 4: Phân số xiên và phân số một dòng

**Files:**
- Modify:
  - `Ast/FormulaNode.cs` (`FracKind { Bar, NoBar, Lin, Skw }`);
  - `Latex/Commands/FractionCommands.cs` (`\nicefrac` → `Skw`);
  - `Omml/OmmlReader.Fractions.cs`, `Omml/OmmlWriter.Fractions.cs`, `Latex/LatexWriter.Fractions.cs`;
  - `Latex/LatexWriter.Scripts.cs` (`WriteScriptBase`), `Latex/LatexWriter.Nary.cs` (`WriteOperand`).
- Test: `tests/FormulaConvert.Tests/FractionTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\nicefrac{a}{b}", F(R("a"), R("b"), "skw"))`; OMML `skw` không còn diagnostic.
  - Phân số `lin` (assert riêng, chỉ một chiều):

    | OMML | LaTeX |
    |---|---|
    | `F(R("a"),R("b"),"lin")` | `a/b`, kèm một `lossy` |
    | `F(R("a+b"),R("c"),"lin")` | `{a+b}/c` |
    | `sSup` có cơ số là `F(R("a"),R("b"),"lin")`, chỉ số `2` | `{a/b}^2` |
    | `F(F(R("a"),R("b"),"lin"), R("c"))` | `\frac{a/b}{c}` |

  - Mỗi chuỗi kết quả trên, `LatexToOmml` không ném. Với `{a/b}^2`, OMML có `sSup` với cơ số là hàng `a/b` (Review Focus 3).
- [ ] **Step 2: Chạy** `dotnet test --filter FractionTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D4 trong spec.
  - Mỗi vế được bọc `{}` trừ khi nó là một `Run` có đúng một `Rune`.
  - `WriteScriptBase` và `WriteOperand` coi `Frac { Kind: Lin }` là không phải atom.
  - `lossy` phát ở reader, không ở writer.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa test `\nicefrac` cũ (đang chờ `lin`); cập nhật bảng Task 4 và "Quy ước" của plan 2026-10-28.
- [ ] **Step 5: Commit** `fix: map \nicefrac to skewed fractions and write linear fractions as a/b`.

### Task 5: Kiểu chữ: `\mathscr` và chữ Hy Lạp toán học

**Files:**
- Modify: `Latex/Commands/StyleCommands.cs` (`ScrCommands`: `("\\mathscr", Scr.Script)` thay cho `\mathcal`), `Omml/MathAlphaDecoder.cs`
- Test: `tests/FormulaConvert.Tests/StyleTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\mathscr{L}", R("L", Scr("script")))`; `\mathcal{L}` cho cùng OMML, Canonical `\mathscr{L}`.
  - Đọc chữ Hy Lạp toán học:

    | Ký tự | Codepoint | LaTeX |
    |---|---|---|
    | `𝛼` | U+1D6FC | `\alpha` |
    | `𝜶` | U+1D736 | `\mathbfit{\alpha}` |
    | `𝚪` | U+1D6AA | `\mathbf{\Gamma}` |
    | `𝛤` | U+1D6E4 | `\Gamma` |
    | `𝚹` | U+1D6B9 | `\mathbf{ϴ}` (U+03F4 ghi trần, vì không có lệnh) |
- [ ] **Step 2: Chạy** `dotnet test --filter StyleTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt.** Thêm các khối, mỗi khối 25 ký tự, vào `MathAlphaDecoder.TryDecode` (giữ chữ ký hiện có, `out char`):

  | Kiểu | Hoa | Thường |
  |---|---|---|
  | đậm | `1D6A8`→U+0391 | `1D6C2`→U+03B1 |
  | nghiêng | `1D6E2`→U+0391 | `1D6FC`→U+03B1 |
  | đậm nghiêng | `1D71C`→U+0391 | `1D736`→U+03B1 |

  Riêng vị trí thứ 18 (offset 17) của mỗi khối hoa trả về U+03F4.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa kỳ vọng `\mathcal` cũ (`StyleTests`, `CoreTests`) và "Quy ước".
- [ ] **Step 5: Commit** `fix: write script letters as \mathscr and decode math Greek letters`.

### Task 6: Dấu phụ rộng, dấu gạch của Word, dấu phẩy trên

**Files:**
- Modify: `Latex/LatexWriter.Accents.cs` (`WriteAccent`), `Omml/OmmlReader.Accents.cs` (`ReadAccent`), `Omml/OmmlReader.Styles.cs` (`ReadRun`)
- Test: `tests/FormulaConvert.Tests/AccentTests.cs`, `tests/FormulaConvert.Tests/ScriptTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\widehat{AB}", Ac("̂", R("AB")))`, và tương tự cho `\widetilde{AB}`, `\widecheck{AB}`, `\overrightarrow{AB}`, `\overleftarrow{AB}`.
  - `\hat{x}` và `\vec{x}` giữ nguyên; `\hat{AB}` có Canonical `\widehat{AB}`.
  - OMML `Ac("\u0305", R("AB"))` → `\overline{AB}`; `Ac("\u0332", R("x"))` → `\underline{x}`.
  - Dấu phẩy trên trong run (`ScriptTests`):
    - OMML `R("f′(x)")` → `f^{\prime}(x)`;
    - OMML `R("g″")` → `g^{\prime\prime}`;
    - OMML `R("′")` đứng một mình → `\prime`.
- [ ] **Step 2: Chạy** `dotnet test --filter "AccentTests|ScriptTests"`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D6 trong spec. "Nhiều hơn một ký tự" nghĩa là `Flatten(body)` có hơn một mục, hoặc một `Run` có hơn một `Rune`. Trong `ReadRun` (run toán, không `m:nor`), một dãy `′″‴` ngay sau một `Rune` khác thành `Script(Run(rune trước), null, Run(dãy prime))`; chuỗi `″` được giữ nguyên trong chỉ số trên.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật "Quy ước".
- [ ] **Step 5: Commit** `fix: use wide accents for multi-letter bodies and read Word overbars and primes`.

### Task 7: Điểm căn hàng `m:aln`, `eqnarray`, `m:brk`

**Files:**
- Modify:
  - `Omml/OmmlReader.cs` (`ReadContent`/`ReadInto` nhận cờ `inEqArrayRow`, chỉ truyền qua vỏ `w:` trong suốt, không truyền vào cấu trúc con);
  - `Omml/OmmlReader.Matrix.cs` (`ReadEqArray`), `Omml/OmmlReader.Styles.cs` (`ReadRun`), `Omml/OmmlWriter.Matrix.cs` (`WriteEqArray`);
  - `Latex/Commands/EnvironmentCommands.cs`: `eqnarray` và `eqnarray*` thành `align`; xóa khỏi mảng `Transparent` và khỏi nhánh `case` trong `ParseEnvironment` (khoảng dòng 62-66).
- Test: `tests/FormulaConvert.Tests/EnvironmentTests.cs`; sửa `EnvironmentTests.Transparent("eqnarray")`.

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\begin{align}x&=1\\\\y&=2\\end{align}", …)`, với OMML mỗi hàng là `R("x") + <m:r><m:rPr><m:aln/></m:rPr><m:t>=1</m:t></m:r>`. Thứ tự con của `m:rPr` theo CT_RPR: `m:lit`, `m:nor` hoặc `m:scr`/`m:sty`, `m:brk`, `m:aln`.
  - Review Focus 4: `Check("\\begin{align}x&\\\\y&\\end{align}", …)` (run `m:aln` với `m:t` rỗng) và `Check("\\begin{align}a&&b\\end{align}", …)`.
  - OMML cũ có `&` nguyên văn trong `m:t` vẫn ra `align`.
  - `\begin{eqnarray}a&=&b\end{eqnarray}` không ném, Canonical `\begin{align}a&=&b\end{align}`.
  - Review Focus 2: `m:aln` trong ô `m:m`, ngay dưới `m:oMath`, và trong `m:f` nằm trong hàng `m:eqArr`, đều bị bỏ qua, không sinh `&`.
  - OMML có `m:brk` → nội dung giữ nguyên, một `lossy`.
- [ ] **Step 2: Chạy** `dotnet test --filter EnvironmentTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D7 trong spec.
  - Reader: trong `ReadRun`, xét `m:aln` **trước** lệnh `return` khi text rỗng.
  - Writer: tách text của run tại mỗi `&` trong hàng `EqArray`. Phần sau `&` thành run mới có `m:aln`, kể cả khi phần đó rỗng.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa dòng "Ký tự `&` … giữ nguyên là `Run("&")`" trong "Quy ước".
- [ ] **Step 5: Commit** `fix: write equation alignment points as m:aln like Word`.

### Task 8: Phantom theo bảng đầy đủ

**Files:**
- Modify: `Omml/OmmlReader.Boxes.cs` (`ReadPhantom`), `Latex/LatexWriter.Boxes.cs` (`WritePhantom`), `Latex/Commands/BoxCommands.cs` (đăng ký `\mathrlap` và `\rlap`), `Data/outofscope.csv` (xóa `\mathrlap` và `\rlap`)
- Test: `tests/FormulaConvert.Tests/BoxTests.cs`; sửa `BoxTests.Writer_shown_phantom_without_a_command_is_the_body` và dòng `\rlap{x}` trong `OutOfScopeTests` (khoảng dòng 53).

- [ ] **Step 1: Viết test đỏ** cho từng dòng của bảng D8 trong spec:
  - `Check("\\mathrlap{x}", "<m:phant><m:phantPr><m:zeroWid m:val=\"1\"/></m:phantPr><m:e>…x…</m:e></m:phant>")`;
  - `\rlap{x}` cho cùng OMML, Canonical `\mathrlap{x}`;
  - OMML có `show` bật cùng `zeroAsc` → `x`, kèm một `lossy`;
  - OMML có `show` tắt cùng `zeroWid` và `zeroAsc` → `\phantom{x}`, kèm một `lossy`.
- [ ] **Step 2: Chạy** `dotnet test --filter BoxTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D8 trong spec. `lossy` phát ở reader.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS.
- [ ] **Step 5: Commit** `feat: map zero-width visible phantoms to \mathrlap`.

### Task 9: Phân loại mọi lệnh MathLive định nghĩa

**Files:**
- Modify:
  - `Data/functions.txt`, `Data/aliases.csv`, `Data/outofscope.csv`, `Data/symbols.csv`;
  - `Latex/Commands/FractionCommands.cs` (`\genfrac`, `\overwithdelims`, `\atopwithdelims`), `Latex/Commands/StyleCommands.cs` (`\mathnormal`);
  - `Latex/Commands/OutOfScopeCommands.cs`: đọc độ dài trần cho `\kern \mkern \mskip \hskip \mspace \lower \raise`;
  - `Latex/CommandClassifier.cs`.
- Test: `tests/FormulaConvert.Tests/CatalogTests.cs`; sửa `FractionTests.Unsupported_or_malformed_throws` (bỏ `\overwithdelims`, `\atopwithdelims`).

**Interfaces:**
- Consumes: `MathLiveCatalog.Defined` (Task 1).

- [ ] **Step 1: Viết test đỏ** `Every_mathlive_defined_command_is_classified`, giống `Every_documented_command_is_classified` nhưng duyệt `MathLiveCatalog.Defined`.
- [ ] **Step 2: Chạy** test. Kết quả mong đợi: FAIL, kèm danh sách lệnh chưa phân loại. Tên rác (không phải tên lệnh) thì sửa công cụ ở Task 1, không phân loại.
- [ ] **Step 3: Cài đặt** theo nhóm. Nhóm không khớp bảng dưới thì chọn `out-of-scope`, và ghi tên lệnh vào báo cáo.

| Nhóm | Lệnh | Cách xử lý |
|---|---|---|
| Hàm | `\deg` và mọi tên `defineFunction` của MathLive tạo `OperatorAtom` | thêm vào `functions.txt` |
| Macro | `\bmod \pmod \mod \bra \ket \braket \Bra \Ket \Braket` | `aliases.csv` theo bảng D9 của spec |
| Macro | `\hermitianconjugate \transpose \conjugatetranspose`, các lệnh dấu hai chấm (`\colondash` …), mũi tên harpoon dài | chép định nghĩa MathLive; thay `\mathrel{…}` / `\mathinner{…}` bằng nội dung bên trong, và `\mkern…` bằng không gì cả |
| Ký hiệu | `\/` | `symbols.csv` `\/,002F`, đặt sau dòng chuẩn của U+002F nếu có |
| Chữ và ký hiệu văn bản | `\ss \oe \OE \ae \AE \o \O \i \j \textbackslash \textdollar…` | `aliases.csv` arity 0 thay bằng ký tự, nên không có đường đọc ngược |
| Phân số có ngoặc | `\genfrac{l}{r}{dày}{kiểu}{n}{d}` | có ngoặc thì `Delimited(l, r, "", [Frac])`; dày `0pt` thì `NoBar`; kiểu khác rỗng thì bỏ kèm `lossy` |
| | `\overwithdelims` / `\atopwithdelims` | infix đọc hai token ngoặc |
| Độ dài | `\kern \mkern \mskip \hskip \mspace \lower \raise` | `out-of-scope`; đối số là `{…}` hoặc độ dài trần `-?[0-9.]+ ?(em\|ex\|pt\|mu\|px\|cm\|mm\|in)`; `\lower`/`\raise` giữ đối số nội dung kế tiếp |
| Bỏ vỏ | `\mathnormal` | như `\mathit` |
| | `\mathstrut \ensuremath \style \htmlClass \htmlId \mathchoice \slashed \stackbin \displaylines \obeyspaces` | `outofscope.csv`, arity và đối số giữ lại theo chữ ký `defineFunction` |

- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Thêm mỗi nhóm một ca vào test tương ứng:
  - `Check("\\genfrac{(}{)}{0pt}{}{a}{b}", <OMML của \binom{a}{b}>, "\\binom{a}{b}")`;
  - `a\bmod b` → `R("a\u2004") + R("mod", Sty("p")) + R("\u2004b")`, không có diagnostic;
  - `\deg x` → `m:func`;
  - `\kern1em x` → `R("x")`, một `out-of-scope`;
  - `\bra{x}` → `R("⟨x|")`, không có diagnostic.
- [ ] **Step 5: Commit** `feat: accept every command MathLive defines`.

### Task 10: Hồi quy theo 21 bảng Word và cập nhật tài liệu

**Files:**
- Create: `tests/FormulaConvert.Tests/WordGalleryTests.cs`
- Modify: `README.md` (phạm vi: MathLive 0.111.1, liên kết spec)

- [ ] **Step 1: Viết test** `Word_gallery_formula_round_trips` (Theory). Mỗi ca:
  - `LatexToOmml` không ném và không có diagnostic `out-of-scope`;
  - `OmmlToLatex` của kết quả, parse lại, cho cùng OMML;
  - mọi lệnh trong LaTeX đầu ra thuộc `MathLiveCatalog.Defined`.

  Các ca, theo từng bảng:

| Bảng | Ca |
|---|---|
| Equation dựng sẵn | `A=\pi r^2`; `(x+a)^n=\sum_{k=0}^{n}\binom{n}{k}x^k a^{n-k}`; `(1+x)^n=1+\frac{nx}{1!}+\frac{n(n-1)x^2}{2!}+\cdots`; `f(x)=a_0+\sum_{n=1}^{\infty}\left(a_n\cos\frac{n\pi x}{L}+b_n\sin\frac{n\pi x}{L}\right)`; `a^2+b^2=c^2`; `x=\frac{-b\pm\sqrt{b^2-4ac}}{2a}`; `e^x=1+\frac{x}{1!}+\frac{x^2}{2!}+\frac{x^3}{3!}+\cdots,\quad -\infty<x<\infty`; `\sin\alpha\pm\sin\beta=2\sin\frac{1}{2}(\alpha\pm\beta)\cos\frac{1}{2}(\alpha\mp\beta)`; `\cos\alpha+\cos\beta=2\cos\frac{1}{2}(\alpha+\beta)\cos\frac{1}{2}(\alpha-\beta)` |
| Fraction | `\frac{dy}{dx}`; `\nicefrac{a}{b}`; `\frac{\partial y}{\partial x}`; `\frac{\pi}{2}` |
| Script | `x_{y^2}`; `e^{-i\omega t}`; `{}_1^n Y` |
| Radical | `\sqrt[3]{x}`; `\sqrt{a^2+b^2}` |
| Integral | `\int_0^1 x\,dx`; `\int\limits_0^1 x\,dx`; `\iiint_V f`; `\oint_C F`; `\oiint_S F`; `\oiiint_V F` |
| Large Operator | `\sum\limits_{i=1}^n i`; `\prod_i x_i`; `\bigcup_{i=1}^n A_i`; `\bigwedge_i p_i` |
| Bracket | `\left\lfloor x\right\rfloor`; `\left\lceil x\right\rceil`; `\left[a,b\right[`; `\left\llbracket x\right\rrbracket`; `\left\langle a\middle|b\middle|c\right\rangle`; `\left.x\right|`; `\begin{cases}-x&x<0\\x&x\ge 0\end{cases}`; `\left\langle{n \atop k}\right\rangle` |
| Function | `\sin^{-1}\theta`; `\sech x`; `\tan\theta=\frac{\sin\theta}{\cos\theta}` |
| Accent | `\dddot{x}`; `\overline{\overline{x}}`; `\overbrace{a+b}^{n}`; `\underbrace{x+y}_{m}`; `\overleftrightarrow{AB}`; `\overrightharpoon{x}`; `\boxed{a^2=b^2+c^2}`; `\overline{x\oplus y}` |
| Limit and Log | `\log_b x`; `\lim_{n\to\infty}\left(1+\frac{1}{n}\right)^n`; `\max_{0\le x\le 1}xe^{-x^2}` |
| Operator | `\overset{\text{def}}{=}`; `\measeq`; `\triangleq`; `\xrightarrow{yields}`; `\xrightarrow{\Delta}`; `\xLeftarrow[b]{a}` |
| Matrix | `\begin{pmatrix}1&\cdots&1\\\vdots&\ddots&\vdots\end{pmatrix}`; `\begin{Vmatrix}a&b\\c&d\end{Vmatrix}`; `\begin{matrix}1&\\&1\end{matrix}` |
| Ký hiệu | `a\nleq b\approx c\parallel d`; `\mathbb{R}\mathscr{L}\mathfrak{g}`; `a\mathrel{≢}b`; `\int f\,dx` |

- [ ] **Step 2: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Ca nào đỏ là lỗi thật: quay lại task sở hữu phần đó, sửa theo TDD.
- [ ] **Step 3: Cập nhật** `README.md`.
- [ ] **Step 4: Commit** `docs: describe MathLive fidelity scope` (gồm test hồi quy).
