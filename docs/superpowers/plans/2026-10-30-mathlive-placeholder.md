# MathLive `\placeholder{}` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `\placeholder{}` của MathLive đi hai chiều OMML ↔ LaTeX. Trong Word nó là đối số rỗng (ô nhập chấm chấm), và đối số bắt buộc rỗng trong OMML đọc ra lại `\placeholder{}`.

**Architecture:** Thêm nút AST `Placeholder`. Task 1 làm chiều LaTeX → OMML: parser nhận lệnh, `OmmlWriter` ghi slot rỗng hoặc `m:box` rỗng, `LatexWriter` biết in `\placeholder{}`. Task 2 làm chiều OMML → LaTeX: `OmmlReader` đọc slot bắt buộc rỗng thành `Placeholder`, có danh sách ngoại lệ, rồi cập nhật các test cũ và mục "Quy ước" của plan gốc.

**Tech Stack:** C#, .NET 10, `System.Xml.Linq`, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-30-mathlive-placeholder-design.md`. Đọc §4 và §5 trước khi làm.

## Global Constraints

- Lệnh `dotnet build` / `dotnet test`. `dotnet test` phải xanh trước mỗi commit.
- AST giữ `internal`, và `PublicSurfaceTests` phải xanh.
- `LatexWriter` chỉ xuất lệnh có trong `tests/FormulaConvert.Tests/Data/mathlive-defined.txt` (`MathLiveOutputTests`).
- Mỗi lệnh trong `mathlive-commands.txt` thuộc đúng một nhóm (`CatalogTests`).
- OMML tối giản, theo thứ tự ECMA-376. Không sinh `m:m`/`m:mr`/`m:eqArr` rỗng.
- Đầu vào không tin cậy chỉ được ném `FormulaParseException`.
- Commit thẳng lên `main`, tiền tố `feat:`/`fix:`/`chore:`/`docs:`. Không commit `bin/`, `obj/`, `.superpowers/`.
- Test dùng `CaseRunner.Check(new ConversionCase(latex, ommlInner, canonical))` và `Xml.R`/`Xml.OMath`/`Xml.SameXml` trong `tests/FormulaConvert.Tests/Harness`.

## Review Focus

1. `\sin`, `\lim_{x\to0}`, `\sum_{i=1}^n` đứng cuối công thức, `\xrightarrow{}` và `\phantom{}` không được biến thành `…\placeholder{}` (spec AG1). Test ở Task 2.
2. `\sqrt{x}` và `\sum` không có cận không được ra `[\placeholder{}]` hay `_{\placeholder{}}` (spec AG2). Test ở Task 2.
3. Đầu vào hỏng quanh lệnh mới (`\placeholder` ở cuối chuỗi, `\placeholder[` thiếu `]`, `\placeholder{` thiếu `}`) phải cho `FormulaParseException`, không phải exception khác. Test ở Task 1.
4. OMML Word có nhiễu trong slot rỗng (`<m:num><m:ctrlPr/></m:num>`, `<m:e><m:r><m:t></m:t></m:r></m:e>`) vẫn tính là rỗng và ra `\placeholder{}`, không sập. Test ở Task 2.
5. `\placeholder{}` trong ngữ cảnh văn bản thô (`\text{\placeholder{}}`, `\operatorname{\placeholder{}}`) chỉ được cho kết quả hoặc `FormulaParseException`, không được ném exception khác. Test ở Task 1.

---

### Task 1: LaTeX → OMML (AST, parser, writers, catalog)

**Files:**
- Modify: `src/FormulaConvert.Core/Ast/FormulaNode.cs` (thêm record)
- Create: `src/FormulaConvert.Core/Latex/Commands/PlaceholderCommands.cs`
- Modify: `src/FormulaConvert.Core/Latex/CommandTable.cs` (gọi `PlaceholderCommands.Register` trong `Default`)
- Modify: `src/FormulaConvert.Core/Omml/OmmlWriter.cs` (`Element`, `WriteNode`)
- Modify: `src/FormulaConvert.Core/Omml/OmmlWriter.Matrix.cs` (`WriteEqArray`/`WriteEqArrayRow`: hàng chỉ có `Placeholder` thì ghi `m:e` rỗng)
- Modify: `src/FormulaConvert.Core/Latex/LatexWriter.cs` (`WriteNode`), và `LatexWriter.Scripts.cs` nếu cần ép bọc `{}`
- Modify: `src/FormulaConvert.Core/Data/mathlive-commands.txt` (thêm `\placeholder` ở cuối file)
- Modify: `tests/FormulaConvert.Tests/Data/mathlive-defined.txt` (thêm `\placeholder` đúng thứ tự sắp xếp, giữa `\pi` và `\pmod`)
- Test: `tests/FormulaConvert.Tests/PlaceholderTests.cs` (mới)

**Interfaces:**
- Produces: `internal sealed record Placeholder : FormulaNode;` (namespace `FormulaConvert.Ast`). Task 2 dùng nó.
- Produces: `internal static class PlaceholderCommands { public static void Register(CommandTable t); }`
- Consumes: `LatexParser.ReadRawOptional()`, `LatexParser.ReadArgument()`, `LatexParser.Position`, `LatexParser.Diagnostics` (đã có).

- [ ] **Step 1: Viết test thất bại** trong `PlaceholderTests.cs`. Task này chỉ assert chiều LaTeX → OMML, vì reader chưa đổi nên chưa dùng `CaseRunner`. Dùng helper `private static void Omml(string latex, string inner) => Assert.True(Xml.SameXml(Xml.OMath(inner), FormulaConverter.LatexToOmml(latex).Output));`

  | Test | Latex | OMML inner kỳ vọng |
  |---|---|---|
  | `Alone_is_empty_box` | `\placeholder{}` | `<m:box><m:e></m:e></m:box>` |
  | `In_row_is_empty_box` | `a+\placeholder{}` | `R("a+")<m:box><m:e></m:e></m:box>` |
  | `Fraction_slot_is_empty` | `\frac{\placeholder{}}{2}` | `<m:f><m:num></m:num><m:den>R("2")</m:den></m:f>` |
  | `Mixed_slot_keeps_box` | `\frac{a\placeholder{}}{2}` | `<m:f><m:num>R("a")<m:box><m:e></m:e></m:box></m:num><m:den>R("2")</m:den></m:f>` |
  | `Sup_slot_is_empty` | `x^{\placeholder{}}` | `<m:sSup><m:e>R("x")</m:e><m:sup></m:sup></m:sSup>` |
  | `Degree_slot_not_hidden` | `\sqrt[\placeholder{}]{x}` | `<m:rad><m:deg></m:deg><m:e>R("x")</m:e></m:rad>` |
  | `Matrix_cells_are_empty` | `\begin{pmatrix}\placeholder{} & \placeholder{}\\ \placeholder{} & \placeholder{}\end{pmatrix}` | `<m:d><m:e><m:m><m:mr><m:e></m:e><m:e></m:e></m:mr><m:mr><m:e></m:e><m:e></m:e></m:mr></m:m></m:e></m:d>` |
  | `Equation_row_is_empty` | `\begin{gathered}\placeholder{}\\b\end{gathered}` | `<m:eqArr><m:e></m:e><m:e>R("b")</m:e></m:eqArr>` |

  Thêm các test sau:
  - `Id_is_dropped_with_lossy`: `LatexToOmml(@"\placeholder[a]{}")` cho OMML giống `\placeholder{}`, và `Assert.Single(Diagnostics)` có `Code == "lossy"`.
  - `Content_is_kept_with_lossy`: `\placeholder{x}` cho OMML `R("x")` và một diagnostic `lossy`.
  - `Id_and_content_warn_once`: `\placeholder[a]{x}` cho đúng một diagnostic.
  - `Malformed_throws` (Theory): `\placeholder`, `\placeholder[a`, `\placeholder{` đều `Assert.Throws<FormulaParseException>`.
  - `Raw_text_contexts_do_not_crash` (Theory): với `\text{\placeholder{}}` và `\operatorname{\placeholder{}}`, `Record.Exception(() => LatexToOmml(x))` là `null` hoặc `FormulaParseException`. Ghi kết quả thực tế vào thân commit.
  - `Writer_prints_placeholder`: `LatexWriter.Write(new Placeholder()) == @"\placeholder{}"`; `LatexWriter.Write(new Script(new Run("x", new RunStyle()), null, new Placeholder())) == @"x^{\placeholder{}}"`; `LatexWriter.Write(new Script(new Placeholder(), null, new Run("2", new RunStyle()))) == @"\placeholder{}^2"`.
- [ ] **Step 2:** Chạy `dotnet test --filter PlaceholderTests`. Kỳ vọng FAIL (`Placeholder` chưa tồn tại, build lỗi).
- [ ] **Step 3: Cài đặt.**
  - Record `Placeholder` theo Interfaces.
  - `PlaceholderCommands.Register`: `t.Add("\\placeholder", …)`. Ghi nhận vị trí lệnh, gọi `ReadRawOptional()` rồi `ReadArgument()`. Nội dung là `FormulaRow { Items.Count: 0 }` thì trả về `new Placeholder()`, ngược lại trả về nội dung. Có id không rỗng (sau `Trim`) hoặc có nội dung thì thêm **một** `Diagnostic("lossy", …, vị trí lệnh)`.
  - `OmmlWriter.Element`: nếu `content is Placeholder` thì trả về phần tử rỗng (giữ `e.Value = ""`).
  - `OmmlWriter.WriteNode`: `Placeholder => new XElement(M + "box", Element("e", FormulaRow.Empty))`.
  - `WriteEqArray`: hàng là `Placeholder` thì ghi `m:e` rỗng (hàng không đi qua `Element`).
  - `LatexWriter.WriteNode`: `case Placeholder: b.Command("\\placeholder"); b.Text("{}"); break;`. Trong `LatexWriter.Scripts.cs`: đối số chỉ số là `Placeholder` thì luôn bọc `{}` (`x^{\placeholder{}}`), còn cơ số là `Placeholder` thì không bọc (`\placeholder{}^2`). Chỉnh `IsSingleAtom` hoặc nơi quyết định bọc cơ số cho đúng cả hai trường hợp.
  - Data: thêm `\placeholder` vào hai file dữ liệu như ở mục Files. Nếu có `mathlive.mjs` 0.111.1 thì chỉ đọc text để xác nhận, không chạy.
- [ ] **Step 4:** Chạy `dotnet test`. Kỳ vọng `PlaceholderTests`, `CatalogTests`, `MathLiveOutputTests` và `PublicSurfaceTests` đều PASS. Toàn bộ test cũ vẫn xanh vì reader chưa đổi.
- [ ] **Step 5: Commit.**
  ```bash
  git add src/FormulaConvert.Core tests/FormulaConvert.Tests
  git commit -m "feat: parse \placeholder{} and write it as an empty OMML argument"
  ```
  Thân commit nêu: `m:box` rỗng dựa trên ECMA-376 §22.1.2.13, đối số rỗng hợp lệ theo `CT_OMathArg` (§22.1.2), và việc Word vẽ ô nhập trong `m:box` rỗng chưa được kiểm chứng (không có Word).

---

### Task 2: OMML → LaTeX (reader, test cũ, tài liệu)

**Files:**
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.cs` (helper `ReadSlot`, nhánh `"box"`)
- Modify: `src/FormulaConvert.Core/Omml/OmmlReader.Fractions.cs`, `OmmlReader.Scripts.cs`, `OmmlReader.Nary.cs`, `OmmlReader.Delimiters.cs`, `OmmlReader.Matrix.cs`, `OmmlReader.Accents.cs`, `OmmlReader.Boxes.cs` (và nơi đọc `m:limLow`/`m:limUpp`, `m:eqArr`)
- Modify: test cũ theo §6 của spec (đã biết: `FractionTests.Empty_frac`, `ScriptTests.Empty_sup_arg`, `Sqrt_empty`, `Empty_group_with_script_then_end`, các test `a{}^2` ở `ScriptTests` quanh dòng 187–193, `BoxTests.Reader_logical_box_reads_body_without_diagnostics` với dữ liệu `<m:box><m:boxPr/></m:box>`, và mọi test ma trận có ô trống hoặc hàng thiếu cột). `AccentTests` dòng 123 (`\overset{}{\rightarrow}`) **không** được đổi, vì `m:lim` là ngoại lệ.
- Modify: `docs/superpowers/plans/2026-10-28-omml-latex-converter.md` (mục "Quy ước")
- Test: `tests/FormulaConvert.Tests/PlaceholderTests.cs`

**Interfaces:**
- Consumes: `Placeholder` (Task 1).
- Produces: `private static FormulaNode ReadSlot(XElement? el, List<Diagnostic> d)` trong `OmmlReader`. Gọi `ReadContent(el, d)`, rồi nếu kết quả là `FormulaRow { Items.Count: 0 }` thì trả về `new Placeholder()`.

- [ ] **Step 1: Viết test thất bại.** Đổi các test trong bảng của Task 1 sang `CaseRunner.Check` (thêm `canonical` khi khác `latex`), rồi thêm:

  | Test | Latex | OmmlInner | Canonical |
  |---|---|---|---|
  | `Empty_frac_reads_placeholders` | `\frac{}{}` | `<m:f><m:num></m:num><m:den></m:den></m:f>` | `\frac{\placeholder{}}{\placeholder{}}` |
  | `Base_slot` | `\placeholder{}^2` | `<m:sSup><m:e></m:e><m:sup>R("2")</m:sup></m:sSup>` | |
  | `Sqrt_body` | `\sqrt{\placeholder{}}` | `<m:rad><m:radPr><m:degHide m:val="1"/></m:radPr><m:deg></m:deg><m:e></m:e></m:rad>` | |
  | `Delimiter_body` | `\left(\placeholder{}\right)` | `<m:d><m:e></m:e></m:d>` | |
  | `Function_arg_stays_empty` | `\sin` | `<m:func><m:fName>R("sin", "<m:sty m:val=\"p\"/>")</m:fName><m:e></m:e></m:func>` | |
  | `Function_arg_placeholder_is_dropped` | `\sin\placeholder{}` | như trên | `\sin` |
  | `Nary_body_stays_empty` | `\sum_{i=1}^n` | OMML hiện tại của `\sum_{i=1}^n` | |
  | `Nary_body_placeholder_is_dropped` | `\sum\placeholder{}` | `m:nary` ∑ với `subHide`, `supHide`, `m:e` rỗng | `\sum` |
  | `Hidden_degree_is_not_placeholder` | `\sqrt{x}` | OMML hiện tại | |
  | `Prescript_empty_stays_absent` | `{}_a x` | OMML hiện tại (`SPre`) | `{}_a x` |
  | `Sparse_matrix_cells` | `\begin{pmatrix}1&\\&1\end{pmatrix}` | `m:d` > `m:m`, ô trống là `<m:e></m:e>` | `\begin{pmatrix}1&\placeholder{}\\\placeholder{}&1\end{pmatrix}` |
  | `Ragged_matrix_padding` | `\begin{matrix}a&b\\c\end{matrix}` | hàng 2 là `R("c")` và `<m:e></m:e>` | `\begin{matrix}a&b\\c&\placeholder{}\end{matrix}` |
  | `Empty_base_reads_placeholder` | `{}^2` | `<m:sSup><m:e></m:e><m:sup>R("2")</m:sup></m:sSup>` | `\placeholder{}^2` |
  | `Isotope_prescript_unchanged` | `{}^{14}C` | OMML hiện tại (`m:sPre`) | `{}^{14}C` |
  | `Arrow_label_stays_empty` | `\xrightarrow{}` | OMML hiện tại (`m:limUpp`, `m:lim` rỗng) | `\overset{}{\rightarrow}` |
  | `Phantom_stays_empty` | `\phantom{}` | OMML hiện tại | `\phantom{}` |
  | `Function_name_stays_empty` | (chỉ đọc) `<m:func><m:fName></m:fName><m:e>R("x")</m:e></m:func>` | | không chứa `\placeholder`, và kết quả parse lại ra đúng OMML đó |

  Thêm các test chỉ đọc, dùng `FormulaConverter.OmmlToLatex(Xml.OMath(inner)).Output`:
  - `<m:box><m:boxPr/></m:box>` ra `\placeholder{}`.
  - `<m:f><m:num>R("a")</m:num></m:f>` (thiếu `m:den`) ra `\frac{a}{\placeholder{}}`.
  - `<m:f><m:num><m:ctrlPr/></m:num><m:den><m:r><m:t></m:t></m:r></m:den></m:f>` ra `\frac{\placeholder{}}{\placeholder{}}`.
  - `<m:sPre><m:sub></m:sub><m:sup>R("2")</m:sup><m:e>R("X")</m:e></m:sPre>` ra như hiện nay, không có `\placeholder`.
  - OMML `m:oMath` rỗng ra `""`.
- [ ] **Step 2:** Chạy `dotnet test --filter PlaceholderTests`. Kỳ vọng FAIL ở bước (b) của `CaseRunner` (đang ra `{}` hoặc rỗng thay vì `\placeholder{}`).
- [ ] **Step 3: Cài đặt `ReadSlot`** và thay `ReadContent` bằng `ReadSlot` đúng tại các slot trong bảng §4.3 của spec. Giữ `ReadContent` cho các ngoại lệ: thân `m:nary`, `m:e` và `m:fName` của `m:func`, `m:lim` của `m:limLow`/`m:limUpp`, `m:e` của `m:phant`, `m:sub`/`m:sup` của `m:sPre`, gốc `m:oMath`. Slot bị ẩn vẫn là `null`. `m:rad` không ẩn bậc thì đọc `m:deg` bằng `ReadSlot`. Nhánh `"box"` trong `OmmlReader.ReadInto`: nếu `ReadContent(m:e)` rỗng (kể cả khi thiếu `m:e`) thì thêm `new Placeholder()`, ngược lại giữ hành vi cũ (đọc xuyên nội dung).
- [ ] **Step 4:** Chạy `dotnet test`. `PlaceholderTests` PASS. Các test cũ bị ảnh hưởng (danh sách ở Files, và bất kỳ test nào khác báo đỏ) thì đổi expected `{}` sang `\placeholder{}`, **trừ** khi test thuộc một ngoại lệ ở Step 3. Nếu một test ngoại lệ đỏ thì lỗi nằm ở code, không phải ở test. Chạy lại đến khi toàn bộ xanh.
- [ ] **Step 5: Cập nhật mục "Quy ước"** trong `docs/superpowers/plans/2026-10-28-omml-latex-converter.md`. Thêm một gạch đầu dòng **Ô nhập (`\placeholder{}`)** tóm tắt: `Placeholder` ↔ đối số rỗng; ở gốc hoặc lẫn trong hàng thì là `m:box` có `m:e` rỗng; reader đọc slot bắt buộc rỗng thành `Placeholder` (kể cả ô ma trận và hàng `m:eqArr`), trừ thân `m:nary`, `m:e`/`m:fName` của `m:func`, `m:lim`, `m:phant`, `m:sPre` và slot ẩn; chỉ số luôn bọc `{\placeholder{}}`; `\placeholder[id]{x}` giữ `x`, bỏ id, báo `lossy`.
- [ ] **Step 6:** Chạy `dotnet test`. Kỳ vọng toàn bộ PASS.
- [ ] **Step 7: Commit.**
  ```bash
  git add src/FormulaConvert.Core tests/FormulaConvert.Tests docs/superpowers/plans/2026-10-28-omml-latex-converter.md
  git commit -m "feat: read empty OMML arguments as \placeholder{}"
  ```
  Thân commit liệt kê những test cũ đã đổi expected.
