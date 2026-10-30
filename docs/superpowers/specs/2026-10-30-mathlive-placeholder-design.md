# Hỗ trợ ô nhập `\placeholder{}` của MathLive (hai chiều OMML ↔ LaTeX)

Ngày: 2026-10-30. Spec gốc của dự án: `2026-10-28-omml-latex-converter-design.md`; quy ước LaTeX chuẩn nằm ở mục "Quy ước" của `docs/superpowers/plans/2026-10-28-omml-latex-converter.md`.

## 1. Bối cảnh và mục đích

Người dùng gõ công thức trong MathLive. Khi chưa gõ xong mà đã lưu, LaTeX còn những ô nhập trống mà MathLive viết là `\placeholder{}`, ví dụ:

```latex
\begin{pmatrix}\placeholder{} & \placeholder{}\\ \placeholder{} & \placeholder{}\end{pmatrix}
```

Hiện tại converter từ chối chuỗi này với lỗi `unknown command \placeholder`.

Mục tiêu là khi chuyển sang OMML và mở bằng Word, mỗi ô `\placeholder{}` hiện ra thành ô vuông chấm chấm để người dùng bấm vào gõ tiếp. Theo chiều ngược lại, khi Word còn ô trống thì LaTeX xuất ra phải có `\placeholder{}` để MathLive hiện lại đúng ô nhập.

**Cơ sở kỹ thuật.** Đối số OMML kiểu `CT_OMathArg` (`m:e`, `m:num`, `m:den`, `m:sub`, `m:sup`, `m:deg`, `m:lim`, `m:fName`) được phép rỗng, vì mọi phần tử con của nó đều có `minOccurs="0"` (ECMA-376 Part 1, §22.1.2). Việc Word vẽ ô nhập chấm chấm cho đối số rỗng là **hành vi của Word**, chuẩn không quy định. Không có Word để kiểm chứng, nên đừng tìm điều khoản ECMA cho chuyện này. Converter đã sinh đối số rỗng cho `{}`: `\frac{}{}` cho ra `<m:f><m:num></m:num><m:den></m:den></m:f>`. Vì vậy phần lớn công việc chỉ là gắn `\placeholder{}` với "đối số rỗng" ở cả hai chiều.

## 2. Mục tiêu, ngoài phạm vi, điều không được xảy ra

**Mục tiêu**
- G1. LaTeX → OMML: `\placeholder{}` ở bất kỳ vị trí nào cũng thành chỗ Word hiện ô nhập.
- G2. OMML → LaTeX: đối số bắt buộc mà rỗng thì xuất `\placeholder{}`, theo quy tắc ở §4.3.
- G3. Round-trip ổn định theo `CaseRunner`: LaTeX → OMML → LaTeX chuẩn, và LaTeX chuẩn → OMML ra lại đúng OMML đó.

**Ngoài phạm vi**
- Chế độ "fill-in-the-blank" của MathLive (`\placeholder[id]{…}` dùng làm prompt có id) không được hỗ trợ đầy đủ, vì Word không có chỗ lưu id. Xem §4.1.
- Không đổi hành vi `m:sPre` (chỉ số trước rỗng vẫn coi là không có), xem §4.3.
- Không thêm tuỳ chọn bật/tắt tính năng. Hành vi mới là mặc định.

**Điều không được xảy ra (anti-goals)**
- AG1. Công thức đầy đủ, không có ô trống, mà lại xuất ra `\placeholder{}`. Điển hình là `\sin`, `\lim_{x\to0}`, `\sum_{i=1}^n` đứng cuối công thức. Hiện chúng sinh `m:e` rỗng (đã kiểm chứng bằng CLI) và **không được** thành `\sin\placeholder{}`. Tương tự, mũi tên không nhãn `\xrightarrow{}` và `\phantom{}` không được có placeholder. Riêng ô trống trong ma trận/`m:eqArr` thì người dùng đã chọn chấp nhận, xem §4.3.
- AG2. Căn bậc hai thường thành `\sqrt[\placeholder{}]{x}`, hoặc `\sum` không có cận thành `\sum_{\placeholder{}}`. Đối số bị ẩn (`degHide`, `subHide`, `supHide`) không phải ô nhập.
- AG3. Sinh OMML sai schema. Không được phát sinh `m:m`, `m:mr`, `m:eqArr` rỗng (quy tắc sẵn có trong AGENTS.md).
- AG4. Đầu vào không tin cậy ném ra exception khác `FormulaParseException`.

## 3. Ràng buộc (từ AGENTS.md, nhắc lại vì liên quan trực tiếp)

- TDD: viết test thất bại trước rồi mới sửa. Test dùng `CaseRunner.Check` / `ConversionCase` trong `tests/FormulaConvert.Tests/Harness`.
- AST là `internal`. `PublicSurfaceTests` phải tiếp tục xanh.
- `LatexWriter` chỉ được xuất lệnh có trong MathLive (`MathLiveOutputTests` kiểm tra bằng `tests/FormulaConvert.Tests/Data/mathlive-defined.txt`).
- Lệnh mới phải có trong bảng dữ liệu và thuộc đúng một nhóm trong `CatalogTests`.
- Writer OMML tối giản, theo thứ tự phần tử ECMA-376. Thay đổi OMML phải dẫn ECMA-376.
- Đổi hành vi writer thì cập nhật mục "Quy ước" trong plan.
- Commit thẳng lên `main`, tiền tố `feat:` / `fix:` / `chore:` / `docs:` (theo AGENTS.md). `dotnet test` phải xanh trước khi commit.

## 4. Thiết kế

### 4.1 AST và parser (LaTeX → AST)

- Thêm nút `internal sealed record Placeholder : FormulaNode;` vào `src/FormulaConvert.Core/Ast/FormulaNode.cs`. Nút này nghĩa là "ô nhập trống".
- Đăng ký lệnh `\placeholder` vào `CommandTable`. Đặt trong một file `Commands/` có sẵn phù hợp, ví dụ `BoxCommands.cs`, hoặc file mới `PlaceholderCommands.cs`, rồi gọi `Register` trong `CommandTable.Default`. Cú pháp MathLive là `\placeholder[id]{nội dung}`:
  1. Đọc đối số tuỳ chọn `[id]` bằng `ReadRawOptional()`, lấy văn bản thô và không parse.
  2. Đọc đối số bắt buộc bằng `ReadArgument()`. Thiếu đối số (`\placeholder` ở cuối chuỗi) thì báo lỗi `FormulaParseException` như mọi lệnh một đối số khác.
  3. Nội dung rỗng (`FormulaRow` không có mục nào) thì trả về `Placeholder`.
  4. Nội dung không rỗng (`\placeholder{x}`) thì trả về chính nội dung đó (giữ nội dung, bỏ vỏ, giống cách xử lý lệnh ngoài phạm vi) và thêm `Diagnostic("lossy", …, vị trí lệnh)`.
  5. Có `[id]` không rỗng thì thêm `Diagnostic("lossy", …)` báo id bị bỏ. Trường hợp vừa có id vừa có nội dung chỉ cần một diagnostic.
- `{}` vẫn parse thành `FormulaRow.Empty` như cũ, không đổi.

### 4.2 OMML writer (AST → OMML)

- **Placeholder đứng một mình trong một đối số** (nội dung của slot, sau khi `Rowify` rút gọn, đúng là `Placeholder`) thì ghi đối số rỗng. Chỗ nên sửa là `OmmlWriter.Element(localName, content)`: nếu `content is Placeholder` thì trả về phần tử rỗng (vẫn ghi dạng `<m:num></m:num>` như hiện nay). Gần như mọi slot đều đi qua `Element`, kể cả ô ma trận, `m:deg` và `m:sub`/`m:sup` của `m:nary`.
  - **Ngoại lệ phải sửa riêng:** hàng của `m:eqArr` **không** đi qua `Element`, vì `WriteEqArray` tự tạo `m:e` rồi gọi `WriteEqArrayRow` (`OmmlWriter.Matrix.cs`). Một hàng chỉ có `Placeholder` (ví dụ `\begin{cases}\placeholder{}\\b\end{cases}`) phải ghi `<m:e></m:e>`, không được ghi `<m:e><m:box>…</m:box></m:e>`.
  - `\sqrt[\placeholder{}]{x}`: `Degree` khác null nên **không** ghi `m:degHide`, và `m:deg` rỗng. Word hiện ô cho bậc căn.
  - `\sum_{\placeholder{}}`: `Sub` khác null nên không ghi `m:subHide`.
- **Placeholder lẫn với nội dung khác, hoặc nằm ở gốc công thức** (ví dụ `a+\placeholder{}`, `\frac{a\placeholder{}}{b}`, hay riêng `\placeholder{}`) thì ghi `<m:box><m:e></m:e></m:box>`. Đây là ô chứa logic `m:box` (ECMA-376 §22.1.2.13), không có `m:boxPr`, với một `m:e` rỗng. Cụ thể là thêm `Placeholder => …` vào `switch` của `OmmlWriter.WriteNode`.
  - Giả định chưa kiểm chứng được (không có Word): Word vẽ ô nhập cho `m:e` rỗng bên trong `m:box` giống như ở mọi đối số khác. Đây là lựa chọn của người viết spec. Ghi lại trong commit message rằng nó dựa trên §22.1.2.13 và đặc tính chung của `CT_OMathArg`.

### 4.3 OMML reader (OMML → AST)

Quy tắc: **đối số bắt buộc mà rỗng (hoặc thiếu hẳn) thì đọc thành `Placeholder`**, trừ các ngoại lệ bên dưới. "Rỗng" nghĩa là `ReadContent(...)` trả về `FormulaRow` không có mục nào. Lưu ý một đối số chỉ chứa phần tử bị bỏ qua, như `m:ctrlPr`, `w:del` hay `m:r` có `m:t` rỗng, cũng tính là rỗng.

Nên thêm một helper, ví dụ `ReadSlot(XElement? el, List<Diagnostic> d)`, rồi thay `ReadContent` bằng nó tại các slot sau:

| Phần tử OMML | Slot đọc thành `Placeholder` khi rỗng |
|---|---|
| `m:f` | `m:num`, `m:den` |
| `m:sSub`, `m:sSup`, `m:sSubSup` | `m:e` (cơ số), `m:sub`, `m:sup` |
| `m:rad` | `m:e`; `m:deg` **chỉ khi** không bị ẩn (`degHide` tắt và `m:deg` có mặt) |
| `m:nary` | `m:sub`/`m:sup` **chỉ khi** không bị ẩn (`subHide`/`supHide` tắt) |
| `m:d` | mỗi `m:e` |
| `m:m` | mỗi ô `m:e` trong `m:mr` |
| `m:eqArr` | mỗi hàng `m:e` |
| `m:acc`, `m:bar`, `m:groupChr`, `m:borderBox` | `m:e` |
| `m:limLow`, `m:limUpp` | `m:e` (chỉ phần thân, không gồm `m:lim`) |
| `m:box` | `m:e` rỗng hoặc thiếu thì cả `m:box` đọc thành `Placeholder` (kể cả khi có `m:boxPr`) |

**Ô trống trong ma trận và `m:eqArr` (quyết định của người dùng: luôn là placeholder).** OMML không phân biệt được ô cố ý để trống với ô chưa điền. Người dùng chọn đọc mọi ô rỗng thành `Placeholder`, vì Word cũng vẽ ô nhập ở đó. Hệ quả được chấp nhận:
- `\begin{pmatrix}1&\\&1\end{pmatrix}` quay về thành `\begin{pmatrix}1&\placeholder{}\\\placeholder{}&1\end{pmatrix}`.
- Hàng thiếu cột được `WriteMatrix` tự chèn ô rỗng (`OmmlWriter.Matrix.cs`), nên `\begin{matrix}a&b\\c\end{matrix}` quay về thành `\begin{matrix}a&b\\c&\placeholder{}\end{matrix}`.

**Ngoại lệ, vẫn đọc thành rỗng như hiện nay (để tránh AG1 và AG2):**
- Thân `m:e` của `m:nary`: `\sum_{i=1}^n` vẫn ra `\sum_{i=1}^n`.
- Đối số `m:e` của `m:func`: `\sin`, `\lim_{x\to0}` vẫn giữ nguyên.
- `m:fName` của `m:func`. Nếu tên hàm là `Placeholder`, `LatexWriter.WriteFunctionApply` sẽ ghi `\placeholder{}x`, và chuỗi này parse lại không còn là hàm nữa (round-trip hỏng).
- `m:lim` của `m:limLow`/`m:limUpp`: `\xrightarrow{}` vẫn ra `\overset{}{\rightarrow}`, `\overset{}{x}` vẫn giữ nguyên (quyết định của người dùng).
- `m:e` của `m:phant`: `\phantom{}` vẫn ra `\phantom{}` (quyết định của người dùng).
- `m:sub`/`m:sup` của `m:sPre`: giữ quy tắc hiện có "rỗng là không có" (`OmmlReader.Scripts.cs`, `NonEmpty`).
- Gốc `m:oMath` rỗng: công thức rỗng vẫn ra chuỗi rỗng.
- Các slot bị ẩn (`degHide`, `subHide`, `supHide`) vẫn là `null` như hiện nay.

Hệ quả: `\sin\placeholder{}` và `\sum\placeholder{}` khi đi LaTeX → OMML có `m:e` rỗng, đọc lại ra `\sin` và `\sum`. Ô nhập vẫn hiện trong Word, chỉ mất khi quay lại LaTeX. Chấp nhận được vì OMML vẫn round-trip đúng. Viết test ghi nhận hành vi này với `CanonicalLatex` tương ứng.

### 4.4 LaTeX writer (AST → LaTeX)

- `Placeholder` thì ghi `\placeholder{}`: `b.Command("\\placeholder")` rồi `b.Text("{}")`. Thêm case vào `LatexWriter.WriteNode`.
- Làm đối số chỉ số thì **luôn bọc `{}`**: `x^{\placeholder{}}`, `x_{\placeholder{}}`, `\sum_{\placeholder{}}`. Đây là ngoại lệ của quy tắc "bỏ `{}` khi là một lệnh ký hiệu", giống `^{\prime}`. Kiểm tra `IsSingleAtom` trong `LatexWriter.Scripts.cs`: có thể `Placeholder` đã rơi vào nhánh có ngoặc, nhưng phải có test.
- Làm cơ số chỉ số thì **không** bọc: `\placeholder{}^2`, không phải `{\placeholder{}}^2`. Nếu writer đang bọc cơ số không phải atom đơn thì `Placeholder` phải được coi là atom đơn khi làm cơ số, nhưng vẫn bọc khi làm đối số chỉ số (dòng trên).
- `m:sSup`/`m:sSub`/`m:sSubSup` có cơ số rỗng thì ra `\placeholder{}` ở cơ số (quyết định của người dùng): `{}^2` và `a{}^2` thành `\placeholder{}^2` và `a\placeholder{}^2`. Lưu ý `{}^{14}C` được parse thành `m:sPre` (đã kiểm chứng bằng CLI), nên vẫn giữ nguyên `{}^{14}C` theo ngoại lệ `m:sPre`.
- Đối số `\frac`, `\sqrt`, `\binom` vốn luôn có `{}`: `\frac{\placeholder{}}{2}`, `\sqrt[\placeholder{}]{x}`.
- Ô ma trận: `\begin{pmatrix}\placeholder{}&\placeholder{}\\\placeholder{}&\placeholder{}\end{pmatrix}`, không có dấu cách quanh `&`, đúng quy ước "nối liền".

### 4.5 Dữ liệu và catalog

- `\placeholder` hiện không có trong `src/FormulaConvert.Core/Data/mathlive-commands.txt` lẫn `tests/FormulaConvert.Tests/Data/mathlive-defined.txt`. Công cụ trích xuất đã bỏ sót nó, nhưng MathLive có xuất lệnh này (ảnh chụp Mathfield Demo của người dùng cho thấy `\begin{pmatrix}\placeholder{} & …`).
- Thêm `\placeholder` bằng tay vào cả hai file, mỗi lệnh một dòng. `tests/FormulaConvert.Tests/Data/mathlive-defined.txt` được sắp theo bảng chữ cái nên chèn đúng chỗ (giữa `\pi` và `\pmod`). `src/FormulaConvert.Core/Data/mathlive-commands.txt` theo thứ tự trong tài liệu MathLive chứ không theo bảng chữ cái, nên thêm vào cuối file. Nếu có sẵn gói `mathlive` 0.111.1 thì xác nhận bằng cách đọc text (`grep '"placeholder"' mathlive.mjs`) và **không chạy mã trong gói**. Không cần sửa `tools/`.
- `CatalogTests`: `\placeholder` phải thuộc đúng một nhóm, ở đây là `Structural` vì nó có trong `CommandTable`. Không được nằm trong `not-in-mathlive.txt`.

### 4.6 Tài liệu

- Thêm một gạch đầu dòng vào mục "Quy ước" của `docs/superpowers/plans/2026-10-28-omml-latex-converter.md`, tóm tắt §4.1–§4.4: `Placeholder` ↔ đối số rỗng / `m:box` rỗng, danh sách ngoại lệ, luật bọc `{}` ở chỉ số, và `\placeholder[id]{x}` là `lossy`.

## 5. Ví dụ chuẩn (dùng làm test `CaseRunner.Check`)

`R(...)` là `Xml.R`. Cột Canonical để trống nghĩa là bằng cột Latex.

| Latex | OmmlInner | Canonical |
|---|---|---|
| `\placeholder{}` | `<m:box><m:e></m:e></m:box>` | |
| `a+\placeholder{}` | `R("a+")<m:box><m:e></m:e></m:box>` | |
| `\frac{\placeholder{}}{2}` | `<m:f><m:num></m:num><m:den>R("2")</m:den></m:f>` | |
| `\frac{}{}` | `<m:f><m:num></m:num><m:den></m:den></m:f>` | `\frac{\placeholder{}}{\placeholder{}}` |
| `x^{\placeholder{}}` | `<m:sSup><m:e>R("x")</m:e><m:sup></m:sup></m:sSup>` | |
| `\placeholder{}^2` | `<m:sSup><m:e></m:e><m:sup>R("2")</m:sup></m:sSup>` | |
| `\sqrt{\placeholder{}}` | `m:rad` có `degHide`, `m:e` rỗng | |
| `\sqrt[\placeholder{}]{x}` | `<m:rad><m:deg></m:deg><m:e>R("x")</m:e></m:rad>` | |
| ma trận trong ảnh (§1) | `m:d` > `m:m` với 4 ô `m:e` rỗng | `\begin{pmatrix}\placeholder{}&\placeholder{}\\\placeholder{}&\placeholder{}\end{pmatrix}` |
| `\left(\placeholder{}\right)` | `<m:d><m:e></m:e></m:d>` | |
| `\sin` | như hiện nay | `\sin` (không có placeholder) |
| `\sin\placeholder{}` | giống `\sin` | `\sin` |
| `\sum_{i=1}^n` | như hiện nay | `\sum_{i=1}^n` |
| `\sqrt{x}` | như hiện nay | `\sqrt{x}` (không có `[\placeholder{}]`) |
| `\begin{cases}\placeholder{}\\b\end{cases}` | `m:d` > `m:eqArr` với hàng đầu `<m:e></m:e>` | |
| `\begin{pmatrix}1&\\&1\end{pmatrix}` | ô trống là `<m:e></m:e>` | `\begin{pmatrix}1&\placeholder{}\\\placeholder{}&1\end{pmatrix}` |
| `\begin{matrix}a&b\\c\end{matrix}` | hàng 2 được chèn thêm ô `<m:e></m:e>` | `\begin{matrix}a&b\\c&\placeholder{}\end{matrix}` |
| `\xrightarrow{}` | như hiện nay | `\overset{}{\rightarrow}` (không có placeholder) |
| `\phantom{}` | như hiện nay | `\phantom{}` |
| `{}^2` | `<m:sSup><m:e></m:e><m:sup>R("2")</m:sup></m:sSup>` | `\placeholder{}^2` |
| `{}^{14}C` | như hiện nay (`m:sPre`) | `{}^{14}C` (không đổi) |

Test ngoài bảng:
- `\placeholder[a]{}` cho ra OMML như `\placeholder{}` và có đúng một diagnostic `lossy`.
- `\placeholder{x}` cho ra OMML như `x` và có một diagnostic `lossy`.
- `\placeholder` không có đối số thì ném `FormulaParseException` có vị trí.
- Reader: `<m:box><m:boxPr/></m:box>` (không có `m:e`) đọc thành `\placeholder{}`. Test hiện có `BoxTests.Reader_logical_box_reads_body_without_diagnostics` với dữ liệu này đang mong đợi `""`, cần đổi.
- Reader: `m:f` thiếu hẳn `m:den` thì ra `\frac{…}{\placeholder{}}`.
- `\placeholder{}` trong ngữ cảnh văn bản thô (`\text{\placeholder{}}`, `\operatorname{\placeholder{}}`) chỉ được cho kết quả hoặc `FormulaParseException`, không được ném exception khác. Người code quyết định kết quả cụ thể và ghi lại bằng test.
- `MathLiveOutputTests` và `CatalogTests` xanh.

## 6. Thay đổi hành vi với test hiện có

Mọi test đang mong đợi `{}` cho một đối số rỗng sau khi đi qua OMML phải đổi sang `\placeholder{}`. Ví dụ: `\frac{}{}`, `x_{}^{}`, `\hat{}`, `\left(\right)`, `\sqrt[]{x}` (thành `\sqrt[\placeholder{}]{x}`), `{}^2` và `a{}^2` (cơ số rỗng thành `\placeholder{}^2`), ma trận có ô trống hoặc hàng thiếu cột. Chạy `dotnet test` để tìm hết, sửa expected và ghi lại trong commit. **Không** đổi expected của các ngoại lệ ở §4.3.

## 7. Quyết định đã chốt với người dùng (2026-10-30)

1. Ô trống trong ma trận và `m:eqArr` luôn là placeholder, kể cả ô do writer tự chèn vào hàng thiếu cột.
2. Nhãn `m:lim` và thân `m:phant` rỗng không phải placeholder.
3. Chỉ số thường (`m:sSup`/`m:sSub`/`m:sSubSup`) không có cơ số thì ra `\placeholder{}` ở cơ số. Chỉ số trước (`m:sPre`, ví dụ `{}^{14}C`) không bị ảnh hưởng.

Lựa chọn của người viết spec, người dùng chưa xác nhận riêng từng điểm: dùng `m:box` rỗng cho placeholder lẫn trong hàng; `\placeholder[id]{x}` giữ `x`, bỏ id, báo `lossy`; các ngoại lệ `m:nary` (thân), `m:func` (`m:e`, `m:fName`) và `m:sPre`.

## 8. Để người code tự quyết

- Tên file và vị trí đăng ký handler, tên helper `ReadSlot`.
- Nội dung chữ của các `Diagnostic.Message`.
- Thứ tự commit, miễn mỗi commit có `dotnet test` xanh.
