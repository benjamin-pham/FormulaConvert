# MathLive Fidelity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** LaTeX do `OmmlToLatex` sinh ra hiển thị đúng trong MathLive 0.111, LaTeX do MathLive sinh ra đọc được trọn vẹn, và OMML sinh ra khớp cách Word đánh dấu (khoảng trắng, điểm căn hàng, phân số xiên).

**Architecture:** Thêm danh sách lệnh MathLive lấy từ mã nguồn (dữ liệu test) và bảng `not-in-mathlive.txt` (dữ liệu runtime) để chặn xuất lệnh MathLive không có. Các sửa còn lại nằm trong từng lát cắt có sẵn (`Latex/Commands/*`, `LatexWriter.*`, `OmmlReader.*`, `OmmlWriter.*`) và bảng CSV trong `src/FormulaConvert.Core/Data/`.

**Tech Stack:** C#, .NET 10, `System.Xml.Linq`, xUnit; Python 3 cho công cụ trích dữ liệu (chỉ đọc văn bản).

**Spec:** `docs/superpowers/specs/2026-10-29-mathlive-fidelity-design.md`

## Global Constraints

- Chỉ `FormulaConverter`, `ConvertResult`, `Diagnostic`, `FormulaParseException` là `public`; AST giữ `internal` (`PublicSurfaceTests`).
- TDD: test đỏ trước, rồi sửa. Test chuyển đổi dùng `CaseRunner.Check` (LaTeX→OMML, OMML→LaTeX chuẩn, parse lại).
- Thêm hoặc đổi lệnh LaTeX thì cập nhật bảng dữ liệu và `CatalogTests` (mỗi lệnh thuộc đúng một nhóm).
- OMML tối giản, đúng thứ tự phần tử ECMA-376; không bao giờ ghi `m:m`, `m:mr`, `m:eqArr` rỗng.
- Đầu vào không tin cậy chỉ được ném `FormulaParseException`; giữ `NestingGuard`, cấm DTD, từ chối code point XML không hợp lệ.
- Công cụ trong `tools/` chỉ đọc file tải về như văn bản, không chạy mã tải về; tải vào thư mục mới, rỗng; chạy Python bằng `python3 -I`.
- Đổi quy ước của writer thì cập nhật mục "Quy ước" trong `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` ngay trong task đó.
- `dotnet test` xanh trước mỗi commit; commit trên `main`, tiền tố `feat:`/`fix:`/`chore:`/`docs:`.

## Review Focus

1. Ký tự khoảng trắng Unicode và ký tự trong `not-in-mathlive.txt` nằm trong `\text{…}` (`m:nor`) phải giữ nguyên văn, không thành `\,` hay `\mathrel{…}` (test ở Task 3 và Task 1).
2. `m:aln` ngoài `m:eqArr` (trong ô `m:m`, trong `m:oMath`) bị bỏ qua, không sinh `&` lạc làm hỏng LaTeX (test ở Task 7).
3. Phân số `lin` làm cơ số chỉ số hoặc tử số (`x^{a+b/c}`, `\frac{a/b}{c}`) phải xuất LaTeX parse lại được (test ở Task 4).
4. Ký tự dấu cách nằm đầu hoặc cuối run toán phải có `xml:space="preserve"` để không mất khi Word đọc (test ở Task 3).
5. `\mathrel{ab}` (nhiều ký tự) vẫn phát `out-of-scope`; chỉ một ký tự mới im lặng (test ở Task 1).

---

### Task 1: Danh sách lệnh MathLive và chặn xuất lệnh MathLive không có

**Files:**
- Create: `tools/extract_mathlive_defined.py`, `tests/FormulaConvert.Tests/Data/mathlive-defined.txt` (sinh ra, `CopyToOutputDirectory`), `src/FormulaConvert.Core/Data/not-in-mathlive.txt` (EmbeddedResource như các bảng khác), `src/FormulaConvert.Core/Data/NotInMathLive.cs`
- Modify: `Data/SymbolTable.cs`, `Data/NaryTable.cs`, `Latex/LatexWriter.cs` (`WriteChar`), `Latex/LatexWriter.Nary.cs` (`WriteFunctionName`, n-ary), `Latex/Commands/OutOfScopeCommands.cs`, `THIRD-PARTY-NOTICES.md`
- Test: `tests/FormulaConvert.Tests/MathLiveOutputTests.cs`

**Interfaces:**
- Produces: `internal static class NotInMathLive { bool TryGetKind(string command, out string kind); }` với `kind` ∈ `rel bin ord fn nary`. `SymbolTable.TryGetCommand(cp)` bỏ qua lệnh có trong `NotInMathLive`; thêm `SymbolTable.TryGetParseOnlyKind(int cp, out string kind)`. Test helper `MathLiveCatalog.Defined` (HashSet lệnh có `\`) và `MathLiveCatalog.Environments`.

- [ ] **Step 1: Sinh danh sách.** Tải `src/latex-commands/*.ts` và `src/core/{parser,modes-math,unicode,tokenizer}.ts` của `arnog/mathlive` nhánh master (khớp npm 0.111.1) bằng `curl` vào một thư mục rỗng mới. Viết `tools/extract_mathlive_defined.py <src_dir> <out>` trích:
  - tên trong `defineFunction([...]|'x')`, `defineSymbols`/mảng `['\\x', 0x…]`;
  - khóa của các bảng macro và `ACCENTS` (`x: 0x…`, `x: '…'`, `'x': …`, `x: { def`);
  - môi trường trong `defineEnvironment`/`defineTabularEnvironment`/`defineRootEnvironment`, ghi thành dòng `env:<tên>`.
  
  Bỏ `mhchem.ts`. Ghi nguồn và giấy phép MIT vào `THIRD-PARTY-NOTICES.md`.
- [ ] **Step 2: Viết test đỏ** trong `MathLiveOutputTests`:
  - `Catalog_has_canaries`: `\frac \nicefrac \left \right \middle \hat \vec \tilde \check \deg \bmod \pmod \mathscr \overrightarrow \widehat \mathrlap \mathrel` và `env:aligned env:gathered env:Vmatrix env:rcases` đều có trong danh sách. Nếu thiếu lệnh nào thì sửa regex của công cụ, không thêm tay.
  - `Every_emitted_command_is_defined_in_mathlive`: với mọi codepoint trong `symbols.csv`, `nary.csv`, `accents.csv` và mọi tên trong `functions.txt`, dựng nút tương ứng (`Run`, `Nary`, `Accent`, `FunctionApply(Run(tên, Plain), Run("x"))`), gọi `LatexWriter.Write`, tách mọi `\\[A-Za-z]+` và `\begin{…}`; mỗi lệnh phải có trong `Defined` và mỗi môi trường trong `Environments`. Thông báo lỗi liệt kê toàn bộ lệnh vi phạm.
  - `Parse_only_symbols_still_parse`: `\nequiv` → `<m:r><m:t>≢</m:t></m:r>`.
  - `Parse_only_relation_is_written_with_mathrel`: OMML `≢` → `a\mathrel{≢}b`; `∟` → `∟`; `\sech x` → `\operatorname{sech}x`. Đọc lại `a\mathrel{≢}b` không có diagnostic.
  - `Mathrel_with_several_characters_still_warns` (Review Focus 5): `\mathrel{ab}` → một `out-of-scope`.
  - `Parse_only_symbol_in_text_is_literal` (Review Focus 1): `\text{≢}` → `\text{≢}`.
- [ ] **Step 3: Chạy** `dotnet test --filter MathLiveOutputTests`. Kết quả mong đợi: FAIL. Test thứ hai liệt kê các lệnh vi phạm, dự kiến ít nhất `\iddots \nequiv \nsimeq \napprox \nasymp \ntlg \ntgl \nsqsubseteq \nsqsupseteq \rightangle \measuredrightangle \varlrtriangle \epar \qed \Euler \dotminus \dlsh \drsh \leftwavearrow \rightwavearrow \sech \csch`.
- [ ] **Step 4: Cài đặt.** Điền `not-in-mathlive.txt` từ danh sách vi phạm, phân loại `kind`:
  - quan hệ và mũi tên → `rel`;
  - `\dotminus` → `bin`;
  - `\iddots \rightangle \measuredrightangle \varlrtriangle \qed \Euler` → `ord`;
  - `\sech \csch` → `fn`;
  - toán tử lớn → `nary`.

  Sửa các writer:
  - `WriteChar`: dùng `TryGetParseOnlyKind`; `rel` thì `\mathrel{c}`, `bin` thì `\mathbin{c}`, còn lại ghi ký tự.
  - `WriteFunctionName`: tên có `kind` là `fn` ghi `\operatorname{…}`.
  - n-ary có `kind` là `nary`: ghi ký tự.

  `OutOfScopeCommands.Handle`: với `\mathrel \mathbin \mathord \mathop \mathopen \mathclose \mathpunct` mà đối số giữ lại là một `Run` một ký tự (một `Rune`) thì không cảnh báo.
- [ ] **Step 5: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa các test cũ đang chờ `\nequiv` và tương tự sang dạng mới.
- [ ] **Step 6: Commit** `fix: never write LaTeX commands MathLive does not define`.

### Task 2: Lệnh chuẩn khi đọc ngược ký hiệu

**Files:**
- Modify: `src/FormulaConvert.Core/Data/symbols.csv` (thứ tự dòng), `tools/extract_symbols.py` (`PREFERRED`)
- Test: `tests/FormulaConvert.Tests/DataTableTests.cs`

**Interfaces:**
- Consumes: `SymbolTable.TryGetCommand` (Task 1).

- [ ] **Step 1: Viết test đỏ** `Canonical_reverse_command` (Theory, `SymbolTable.TryGetCommand(cp)`):
  - `0x2270 \nleq`, `0x2271 \ngeq`, `0x2288 \nsubseteq`, `0x2289 \nsupseteq`;
  - `0x2224 \nmid`, `0x2226 \nparallel`;
  - `0x2A87 \lneq`, `0x2A88 \gneq`, `0x2268 \lneqq`, `0x2269 \gneqq`;
  - `0x2248 \approx`, `0x223C \sim`, `0x2225 \parallel`, `0x2223 \mid`, `0x27F7 \longleftrightarrow`.
- [ ] **Step 2: Chạy** `dotnet test --filter Canonical_reverse_command`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt.** Đưa dòng chuẩn lên trước trong `symbols.csv`; thêm các cặp trên vào `PREFERRED` để lần tái tạo bảng sau giữ nguyên thứ tự.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật kỳ vọng cũ (`\thickapprox`, `\Vert`, `\vert`, `\biconditional`…) và dòng ví dụ ký hiệu chuẩn trong "Quy ước" của plan 2026-10-28.
- [ ] **Step 5: Commit** `fix: write the common LaTeX command for shared symbol codepoints`.

### Task 3: Khoảng trắng hai chiều

**Files:**
- Create: `src/FormulaConvert.Core/Latex/Commands/SpaceCommands.cs`, `src/FormulaConvert.Core/Data/SpaceTable.cs`
- Modify:
  - `Data/outofscope.csv`: bỏ `\quad \qquad \enspace \; \: \ `, giữ `\!` (đổi sang `lossy` trong `SpaceCommands`);
  - `Latex/Commands/OutOfScopeCommands.cs`: bỏ dòng đăng ký `\,`;
  - `Latex/LatexParser.cs`: `~` thành U+00A0;
  - `Latex/LatexWriter.Styles.cs` (`WriteStyledRuns`), `Omml/OmmlWriter.Styles.cs` (`WriteRun`);
  - `Latex/CommandClassifier.cs`: nhóm `Space`.
- Test: `tests/FormulaConvert.Tests/SpaceTests.cs`; sửa `OutOfScopeTests.cs`, `CatalogTests.cs` (thêm `"Space"` vào `CategoryNames`).

**Interfaces:**
- Produces: `internal static class SpaceTable { bool TryGetText(string command, out string text); bool IsSpace(int cp); }`; `LatexWriter.WriteMathText(string text, LatexBuilder b)` (thay vòng lặp `WriteChar` trong `WriteStyledRuns`, gộp hai U+2003 thành `\qquad`).

- [ ] **Step 1: Viết test đỏ** (`CaseRunner.Check`, OMML là một run):

| Latex | `m:t` | Canonical |
|---|---|---|
| `a\,b` | `a b` | |
| `a\:b` | `a b` | |
| `a\;b` | `a b` | |
| `a\quad b` | `a b` | |
| `a\qquad b` | `a  b` | |
| `a\enspace b` | `a b` | |
| `a\ b` | `a b` | |
| `a~b` | `a b` | `a\ b` |

  Thêm các test:
  - OMML `a b`, `a b` → `a\,b`; `a b` → `a\:b`; `a b` (U+0020) → `a\ b`.
  - `a\!b` → run `ab`, một diagnostic `lossy`.
  - `\text{a b}` giữ ` ` nguyên văn (Review Focus 1).
  - `\quad x` → `m:t` có `xml:space="preserve"` (Review Focus 4).
  - `\int f(x)\,dx` → không còn diagnostic.
- [ ] **Step 2: Chạy** `dotnet test --filter SpaceTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D3 trong spec. Trong `WriteRun`, ghi `xml:space="preserve"` khi text có bất kỳ ký tự nào mà `SpaceTable.IsSpace` hoặc `char.IsWhiteSpace` nhận ra. Sau `\ ` và `\,` không cần dấu cách phân tách; sau `\quad` thì `LatexBuilder.Command` đã tự chèn.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật `OutOfScopeTests` và "Quy ước" (thêm dòng khoảng trắng).
- [ ] **Step 5: Commit** `feat: map LaTeX spacing commands to Unicode spaces in OMML`.

### Task 4: Phân số xiên và phân số một dòng

**Files:**
- Modify: `Ast/FormulaNode.cs` (`FracKind { Bar, NoBar, Lin, Skw }`), `Latex/Commands/FractionCommands.cs` (`\nicefrac` → `Skw`), `Omml/OmmlReader.Fractions.cs`, `Omml/OmmlWriter.Fractions.cs`, `Latex/LatexWriter.Fractions.cs`
- Test: `tests/FormulaConvert.Tests/FractionTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\nicefrac{a}{b}", F(R("a"), R("b"), "skw"))`;
  - OMML `F(R("a"),R("b"),"skw")` không còn diagnostic;
  - OMML `F(R("a"),R("b"),"lin")` → `a/b` cùng một `lossy`;
  - OMML `F(R("a+b"),R("c"),"lin")` → `{a+b}/c`;
  - Review Focus 3: `sSup` có `sup` là `F(R("a+b"),R("c"),"lin")` → `x^{{a+b}/c}`, và `LatexToOmml` của chuỗi đó không ném.
- [ ] **Step 2: Chạy** `dotnet test --filter FractionTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D4 trong spec. Writer ghi `Lin` bằng `WriteGroup` khi tử hoặc mẫu có hơn một mục, nối bằng `/`; phát `lossy` ở reader, không ở writer.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật bảng Task 4 và "Quy ước" của plan 2026-10-28 (`\nicefrac` ↔ `skw`).
- [ ] **Step 5: Commit** `fix: map \nicefrac to skewed fractions and write linear fractions as a/b`.

### Task 5: Kiểu chữ: `\mathscr` và chữ Hy Lạp toán học

**Files:**
- Modify: `Latex/Commands/StyleCommands.cs` (`ScrCommands`: `("\\mathscr", Scr.Script)` thay `\mathcal`), `Omml/MathAlphaDecoder.cs`
- Test: `tests/FormulaConvert.Tests/StyleTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\mathscr{L}", R("L", Scr("script")))`;
  - `\mathcal{L}` cho cùng OMML, Canonical `\mathscr{L}`;
  - đọc `𝛼` (U+1D6FC) → `\alpha`; `𝜶` (U+1D736) → `\mathbfit{\alpha}`; `𝚪` (U+1D6AA) → `\mathbf{\Gamma}`; `𝛤` (U+1D6E4) → `\Gamma`.
- [ ] **Step 2: Chạy** `dotnet test --filter StyleTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt.** Giải mã các khối như bảng `MATH_UNICODE_BLOCKS` của MathLive:
  - đậm: `1D6A8`→U+0391, `1D6C2`→U+03B1;
  - nghiêng: `1D6E2`→U+0391, `1D6FC`→U+03B1;
  - đậm nghiêng: `1D71C`→U+0391, `1D736`→U+03B1;
  - mỗi khối 25 ký tự.

  Đổi `TryDecode` thành `TryDecode(Rune rune, out Rune plain, out RunStyle style)` và cập nhật hai nơi gọi (`OmmlReader.ReadRun`, `LatexParser.ParseAtom`).
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật kỳ vọng `\mathcal` cũ và "Quy ước".
- [ ] **Step 5: Commit** `fix: write script letters as \mathscr and decode math Greek letters`.

### Task 6: Dấu phụ rộng và dấu gạch của Word

**Files:**
- Modify: `Latex/LatexWriter.Accents.cs` (`WriteAccent`), `Omml/OmmlReader.Accents.cs` (`ReadAccent`)
- Test: `tests/FormulaConvert.Tests/AccentTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\widehat{AB}", Ac("̂", R("AB")))`, tương tự `\widetilde{AB}`, `\widecheck{AB}`, `\overrightarrow{AB}`, `\overleftarrow{AB}`;
  - `\hat{x}`, `\vec{x}` giữ nguyên;
  - `\hat{AB}` có Canonical `\widehat{AB}`;
  - OMML `Ac("̅", R("AB"))` → `\overline{AB}`; `Ac("̲", R("x"))` → `\underline{x}`.
- [ ] **Step 2: Chạy** `dotnet test --filter AccentTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D6 trong spec. "Nhiều hơn một ký tự" nghĩa là `Flatten(body)` có hơn một mục, hoặc một `Run` có hơn một `Rune`.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Cập nhật "Quy ước".
- [ ] **Step 5: Commit** `fix: use wide accents for multi-letter bodies and read Word overbar accents`.

### Task 7: Điểm căn hàng `m:aln`, `eqnarray`, `m:brk`

**Files:**
- Modify: `Omml/OmmlReader.Matrix.cs` (`ReadEqArray`), `Omml/OmmlReader.Styles.cs` (`ReadRun` nhận cờ `inEqArray`), `Omml/OmmlWriter.Matrix.cs` (`WriteEqArray`), `Latex/Commands/EnvironmentCommands.cs` (`eqnarray`, `eqnarray*` thành `align`, bỏ khỏi `Transparent`)
- Test: `tests/FormulaConvert.Tests/EnvironmentTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\begin{align}x&=1\\\\y&=2\\end{align}", …)`, với OMML mỗi hàng là `R("x") + <m:r><m:rPr><m:aln/></m:rPr><m:t>=1</m:t></m:r>`. Thứ tự con của `m:rPr` theo CT_RPR: `m:lit`, `m:nor` hoặc `m:scr`/`m:sty`, `m:brk`, `m:aln`.
  - OMML cũ có `&` nguyên văn trong `m:t` vẫn ra `align`.
  - `\begin{eqnarray}a&=&b\end{eqnarray}` không ném, Canonical `\begin{align}a&=&b\end{align}`.
  - Review Focus 2: `m:aln` trong ô `m:m` và ngay dưới `m:oMath` bị bỏ qua, không sinh `&`.
  - OMML có `m:brk` → nội dung giữ nguyên, một `lossy`.
- [ ] **Step 2: Chạy** `dotnet test --filter EnvironmentTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D7 trong spec. Writer tách text của run tại mỗi `&` trong hàng `EqArray`. Phần sau `&` thành run mới có `m:aln`; `&` đứng cuối hàng thành run `m:aln` với `m:t` rỗng.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Sửa dòng "Ký tự `&` … giữ nguyên là `Run("&")`" trong "Quy ước".
- [ ] **Step 5: Commit** `fix: write equation alignment points as m:aln like Word`.

### Task 8: Phantom hiện có độ rộng 0

**Files:**
- Modify: `Omml/OmmlReader.Boxes.cs` (`ReadPhantom`), `Latex/LatexWriter.Boxes.cs` (`WritePhantom`), `Latex/Commands/BoxCommands.cs` (đăng ký `\mathrlap` và `\rlap`), `Data/outofscope.csv` (bỏ `\mathrlap` và `\rlap`)
- Test: `tests/FormulaConvert.Tests/BoxTests.cs`

- [ ] **Step 1: Viết test đỏ:**
  - `Check("\\mathrlap{x}", "<m:phant><m:phantPr><m:zeroWid m:val=\"1\"/></m:phantPr><m:e>…x…</m:e></m:phant>")`. `show` mặc định bật nên không ghi.
  - `\rlap{x}` cho cùng OMML, Canonical `\mathrlap{x}`.
  - OMML có `show` bật và `zeroAsc` → `x` cùng một `lossy`.
- [ ] **Step 2: Chạy** `dotnet test --filter BoxTests`. Kết quả mong đợi: FAIL.
- [ ] **Step 3: Cài đặt** theo D8 trong spec.
- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS.
- [ ] **Step 5: Commit** `feat: map zero-width visible phantoms to \mathrlap`.

### Task 9: Phân loại mọi lệnh MathLive định nghĩa

**Files:**
- Modify:
  - `Data/functions.txt`, `Data/aliases.csv`, `Data/outofscope.csv`, `Data/symbols.csv`;
  - `Latex/Commands/FractionCommands.cs` (`\genfrac`, `\overwithdelims`, `\atopwithdelims`), `Latex/Commands/StyleCommands.cs` (`\mathnormal`);
  - `Latex/Commands/OutOfScopeCommands.cs` (đọc đối số độ dài cho `\kern \mkern \mskip \hskip \mspace`);
  - `Latex/CommandClassifier.cs`.
- Test: `tests/FormulaConvert.Tests/CatalogTests.cs`

**Interfaces:**
- Consumes: `MathLiveCatalog.Defined` (Task 1).

- [ ] **Step 1: Viết test đỏ** `Every_mathlive_defined_command_is_classified`, giống `Every_documented_command_is_classified` nhưng duyệt `MathLiveCatalog.Defined`.
- [ ] **Step 2: Chạy** test. Kết quả mong đợi: FAIL, kèm danh sách lệnh chưa phân loại, dự kiến khoảng 50 lệnh. Tên rác do regex bắt nhầm (không xuất hiện trong lời gọi `define*` hay bảng macro của MathLive) thì sửa ở Task 1 công cụ, không phân loại.
- [ ] **Step 3: Cài đặt** theo nhóm:

| Nhóm | Lệnh | Cách xử lý |
|---|---|---|
| Hàm | `\deg` và các tên MathLive đăng ký như hàm (`\arcsec`, `\arccsc`, `\arsinh`…) | thêm vào `functions.txt` |
| Macro MathLive | `\bmod`, `\pmod`, `\mod`, `\bra`, `\ket`, `\braket`, `\Bra`, `\Ket`, `\Braket`, `\hermitianconjugate`, `\transpose`, các lệnh dấu hai chấm, mũi tên harpoon dài | dòng `aliases.csv` chép định nghĩa macro của MathLive. Riêng `mod` viết `\mathrm{mod}`, không dùng `\operatorname`, vì `\operatorname` nuốt toán hạng |
| Chữ và ký hiệu văn bản | `\ss \oe \OE \ae \AE \o \O \i \j \text…` | `aliases.csv` arity 0 thay bằng ký tự, để không có đường đọc ngược |
| Phân số có ngoặc | `\genfrac{l}{r}{dày}{kiểu}{n}{d}` | `Delimited(l, r, "", [Frac])` khi có ngoặc; dày `0pt` thì `NoBar`; bỏ kiểu kèm `lossy` khi khác rỗng |
| | `\overwithdelims` / `\atopwithdelims` | infix đọc hai token ngoặc |
| Độ dài | `\kern \mkern \mskip \hskip \mspace` | `out-of-scope`, đọc đối số `{…}` hoặc độ dài trần `-?[0-9.]+(em\|ex\|pt\|mu\|px\|cm\|mm\|in)` |
| Bỏ vỏ | `\mathnormal` | như `\mathit` |
| | `\mathstrut \ensuremath \style \htmlClass \htmlId \mathchoice \slashed \stackbin \lower \raise \displaylines \obeyspaces` | dòng `outofscope.csv` với arity và đối số giữ lại theo chữ ký `defineFunction` của MathLive |

- [ ] **Step 4: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Thêm vào test tương ứng (`FractionTests`, `NaryFunctionTests`, `OutOfScopeTests`) một ca cho mỗi nhóm: `\genfrac{(}{)}{0pt}{}{a}{b}` ra cùng OMML với `\binom{a}{b}`; `a\bmod b`; `\deg x`; `\kern1em x` → `x` cùng một `out-of-scope`.
- [ ] **Step 5: Commit** `feat: accept every command MathLive defines`.

### Task 10: Kiểm tra hồi quy các mẫu Word và cập nhật tài liệu

**Files:**
- Create: `tests/FormulaConvert.Tests/WordGalleryTests.cs`
- Modify: `README.md` (phạm vi: MathLive 0.111, liên kết spec)

- [ ] **Step 1: Viết test** `Word_gallery_formula_round_trips` (Theory) với 9 công thức dựng sẵn của Word cùng mỗi mẫu cấu trúc trong `word-equation-images/` viết bằng LaTeX:
  - `A=\pi r^2`;
  - `x=\frac{-b\pm\sqrt{b^2-4ac}}{2a}`;
  - `e^x=1+\frac{x}{1!}+\cdots,\quad -\infty<x<\infty`;
  - `\lim_{n\to\infty}\left(1+\frac{1}{n}\right)^n`;
  - `\max_{0\le x\le 1}xe^{-x^2}`;
  - `\overbrace{a+b}^{n}`, `\xrightarrow{yields}`, `\overset{\Delta}{\rightarrow}`;
  - `\begin{pmatrix}1&\cdots&1\\\vdots&\ddots&\vdots\end{pmatrix}`;
  - `\nicefrac{a}{b}`, `\sech x`, `\overline{\overline{x}}`, `\left\langle a\middle|b\right\rangle`, `\iint_D f\,dA`.

  Mỗi ca:
  - `LatexToOmml` không ném và không có diagnostic `out-of-scope`;
  - `OmmlToLatex` của kết quả, parse lại, cho cùng OMML;
  - mọi lệnh trong LaTeX đầu ra thuộc `MathLiveCatalog.Defined`.
- [ ] **Step 2: Chạy** `dotnet test`. Kết quả mong đợi: PASS. Ca nào đỏ là lỗi thật: quay lại task sở hữu phần đó, sửa theo TDD.
- [ ] **Step 3: Cập nhật** `README.md`.
- [ ] **Step 4: Commit** `docs: describe MathLive fidelity scope` (gồm test hồi quy).
