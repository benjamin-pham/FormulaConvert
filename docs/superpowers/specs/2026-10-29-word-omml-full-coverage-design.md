# Bổ sung độ phủ 100% công thức Microsoft Word (OMML ↔ LaTeX) — Thiết kế

Ngày: 2026-10-29

## 1. Mục tiêu

Nâng cấp `FormulaConvert` để hỗ trợ trọn vẹn hai chiều (`LaTeX ↔ OMML`) cho toàn bộ các mẫu công thức (Structures) và bảng ký hiệu (Symbols) có trên thanh công cụ Equation của Microsoft Word (đối chiếu qua 21 bảng giao diện Word và đặc tả ECMA-376 Part 1 §22.1).

Tiêu chí thành công:
- Đọc được toàn bộ 18/18 phần tử cấu trúc OMML của ECMA-376 §22.1.2 (bổ sung `m:box` và thuộc tính đối số `m:argPr`) mà không phát cảnh báo `unsupported-omml`.
- Mũi tên co giãn có chữ (`\xrightarrow`, `\xRightarrow`... — 12 mẫu *Operator Structures* của Word) và ngoặc gộp có nhãn (`\overbrace{x}^{y}`, `\underbrace{x}_{y}`) sinh ra OMML dùng `<m:groupChr>` / `<m:limUpp>` / `<m:limLow>` đúng chuẩn Word để tự kéo giãn trong Word, đồng thời đọc ngược về LaTeX chuẩn từ cả định dạng `<m:groupChr>` của Word lẫn `<m:limUpp>`/`<m:limLow>` cũ.
- Bảng hàm, toán tử lớn, dấu phụ và ký hiệu đơn bao phủ đầy đủ các mục trên giao diện Word; loại bỏ hoàn toàn 14 mã Private Use Area (`U+E006..U+E01B`) gây lỗi hiển thị ô vuông trên Word; tự động giải mã các ký tự Unicode kiểu chữ (`U+1D400..U+1D7FF` và Letterlike BMP) khi đọc từ Word sang lệnh kiểu chữ LaTeX (`\mathbb`, `\mathcal`, `\mathfrak`, `\mathbf`, `\mathsf`, `\mathtt`).

## 2. Các thay đổi về cấu trúc OMML và AST

### 2.1 Phần tử `m:box` và `m:argPr` (ECMA-376 §22.1.2.10 & §22.1.2.6)

- **`m:argPr` (Argument Properties):**
  - Xuất hiện bên trong `<m:num>`, `<m:den>`, `<m:e>`... khi người dùng chọn mẫu *Small Fraction* (`<m:argPr><m:argSz m:val="-2"/></m:argPr>`).
  - Trong `OmmlReader.ReadInto`: thêm `case "argPr": break;` (bỏ qua thuộc tính trình bày của đối số như `ctrlPr`, không sinh cảnh báo `unsupported-omml`).
- **`m:box` (Box Object):**
  - Xuất hiện khi người dùng chọn *Differentials* (`dx`, `dy`, `d\theta` với `<m:diff m:val="1"/>`) hoặc *Basic Operators* (`:=`, `==`, `+=`, `-=` với `<m:opEmu m:val="1"/>`).
  - Trong `OmmlReader.ReadInto`: thêm `case "box": ReadInto(el.Element(M + "e"), items, d); break;` để đọc xuyên qua hộp logic và lấy nội dung toán học trong `<m:e>`, bỏ qua `<m:boxPr>`.

### 2.2 Ngoặc gộp có nhãn (`\overbrace{x}^{y}`, `\underbrace{x}_{y}`)

- **AST & Parser (`LatexParser` / `ScriptParsing`):**
  - Khi một nút `GroupChar("⏞", Top: true, x, VertJcTop: false)` (`\overbrace{x}`) được gắn chỉ số trên `^y` (và không có chỉ số dưới), `ScriptParsing.Attach` chuyển thành `Limit(GroupChar("⏞", true, x), y, Upper: true)`.
  - Khi một nút `GroupChar("⏟", Top: false, x, VertJcTop: false)` (`\underbrace{x}`) được gắn chỉ số dưới `_y` (và không có chỉ số trên), `ScriptParsing.Attach` chuyển thành `Limit(GroupChar("⏟", false, x), y, Upper: false)`.
- **OMML Writer (`OmmlWriter`):**
  - Sinh `<m:limUpp><m:e><m:groupChr>...</m:groupChr></m:e><m:lim>y</m:lim></m:limUpp>` cho `\overbrace{x}^{y}` và `<m:limLow>` tương ứng cho `\underbrace{x}_{y}`, khớp hoàn toàn với mẫu trong gallery *Accent* của Word.
- **LaTeX Writer (`LatexWriter.WriteLimit`):**
  - Khi gặp `Limit(GroupChar("⏞", Top: true, x, VertJcTop: false), y, Upper: true)`, xuất ra `\overbrace{x}^y` (dùng quy tắc bọc `{}` của đối số chỉ số).
  - Khi gặp `Limit(GroupChar("⏟", Top: false, x, VertJcTop: false), y, Upper: false)`, xuất ra `\underbrace{x}_y`.

### 2.3 Mũi tên co giãn có chữ (`\xrightarrow[below]{above}`, `\xRightarrow`...)

- **Mô hình AST (`GroupChar`):**
  - Mở rộng record thành `internal sealed record GroupChar(string Chr, bool Top, FormulaNode Body, bool VertJcTop = false) : FormulaNode;`.
  - Ý nghĩa theo ECMA-376 §22.1.2.41:
    - Với ngoặc gộp thông thường (`\overbrace`, `\underbrace`, `\overparen`, `\underparen`, `\underrightarrow`...): `VertJcTop = false` (không ghi `<m:vertJc>` khi xuất OMML; `m:e` là thân chính, `m:chr` nằm ở `pos`).
    - Với mũi tên co giãn của Word (*Operator Structures*): `m:e` là nhãn chữ, `m:chr` là ký tự mũi tên co giãn:
      - Nhãn ở trên, mũi tên ở dưới (`\xrightarrow{above}`): `Top = false` (`m:pos="bot"`), `VertJcTop = true` (`m:vertJc="top"`).
      - Mũi tên ở trên, nhãn ở dưới (`\xrightarrow[below]{}`): `Top = true` (`m:pos="top"`), `VertJcTop = true` (ghi `<m:vertJc m:val="bot"/>` để phân biệt với ngoặc gộp trên).
      - Có cả nhãn trên và nhãn dưới (`\xrightarrow[below]{above}`): bọc `Limit(new GroupChar(chr, Top: false, above, VertJcTop: true), below, Upper: false)` (tức `<m:limLow>` bọc quanh `<m:groupChr>`).
- **Chiều `OMML → LaTeX` (`OmmlReader.ReadGroupChar` & `LatexWriter`):**
  - `OmmlReader.ReadGroupChar`: đọc thêm `<m:vertJc m:val="top|bot"/>`. Đánh dấu `VertJcTop = true` khi:
    - `pos == "bot"` và `vertJc == "top"`, hoặc
    - `pos == "top"` và `vertJc == "bot"` trên một ký tự mũi tên thuộc bảng `OverUnderCommands`.
  - `LatexWriter`:
    - Trong `WriteGroupChar`: nếu `g.VertJcTop` (hoặc `g.Chr` là mũi tên như `⇐, ⇒, ↔, ⇔` không nằm trong `AccentCommands.GroupChars`) và `OverUnderCommands.TryGetArrowCommand(g.Chr, out var arrowCmd)` (trừ `=`):
      - Nếu `!g.Top`: xuất `\xrightarrow{Body}`.
      - Nếu `g.Top`: xuất `\xrightarrow[Body]{}`.
    - Trong `WriteLimit`: nếu gặp `Limit(GroupChar { VertJcTop: true } g, below, Upper: false)` với `OverUnderCommands.TryGetArrowCommand(g.Chr, out var arrowCmd)`: xuất `\xrightarrow[below]{g.Body}`.
    - Giữ nguyên nhánh nhận diện `Limit(Run(arrow), ...)` hiện tại để tương thích ngược với mọi OMML dùng `<m:limUpp>`/`<m:limLow>` trực tiếp trên `<m:r>`.

## 3. Chuẩn hóa bảng dữ liệu & Unicode

### 3.1 Bảng hàm (`functions.txt`), toán tử lớn (`nary.csv`) và dấu phụ (`accents.csv`)

1. **`functions.txt`:**
   - Thêm `\sech` và `\csch` để hỗ trợ 4 mẫu `sech`, `csch`, `sech⁻¹`, `csch⁻¹` trong gallery *Function* của Word.
2. **`nary.csv`:**
   - Thêm 4 toán tử từ gallery *Basic N-ary Operators* của Word:
     - `\intclockwise,2231,true` (`∱`)
     - `\varointclockwise,2232,true` (`∲`)
     - `\ointctrclockwise,2233,true` (`∳`)
     - `\intctrclockwise,2A11,true` (`⨑`)
3. **`accents.csv` & `outofscope.csv`:**
   - Chuyển `\overleftharpoon` (`U+20D0` `⃐`) và `\overrightharpoon` (`U+20D1` `⃑`) từ `outofscope.csv` sang `accents.csv` (vì gallery *Accent* của Word hỗ trợ trực tiếp bằng `<m:acc>`).
   - Trong `OmmlReader.ReadAccent`: nếu gặp `m:acc` có `chr = "\u033F"` (gạch kép trên đầu `̿` trong gallery *Accent* của Word), chuyển thành hai lớp `new Bar(true, new Bar(true, body))` (`\overline{\overline{...}}`) để hiển thị đúng trên cả Word lẫn MathLive.

### 3.2 Loại bỏ mã Private Use Area (PUA) và bổ sung ký hiệu Word trong `symbols.csv`

1. **Thay 14 mã PUA (`U+E006..U+E01B`) bằng mã Unicode chuẩn:**
   - `\nshortmid`: `E006` → `2224` (`∤`)
   - `\nshortparallel`: `E007` → `2226` (`∦`)
   - `\lvertneqq`: `E00C` → `2268` (`≨`)
   - `\gvertneqq`: `E00D` → `2269` (`≩`)
   - `\ngeqq`: `E00E` → `2271` (`≱`)
   - `\ngeqslant`: `E00F` → `2A88` (`⪈`)
   - `\nleqslant`: `E010` → `2A87` (`⪇`)
   - `\nleqq`: `E011` → `2270` (`≰`)
   - `\nsubseteqq`: `E016` → `2288` (`⊈`)
   - `\varsubsetneqq`: `E017` → `2ACB` (`⫋`)
   - `\nsupseteqq`: `E018` → `2289` (`⊉`)
   - `\varsupsetneqq`: `E019` → `2ACC` (`⫌`)
   - `\varsubsetneq`: `E01A` → `228A` (`⊊`)
   - `\varsupsetneq`: `E01B` → `228B` (`⊋`)
   - Cập nhật `tools/extract_symbols.py` để tự động ánh xạ các mã PUA này khi tái tạo bảng.

2. **Chuẩn hóa dấu trừ Unicode (`U+2212` `−`):**
   - Trong `OmmlReader.ReadRun` (khi không phải `m:nor`) và `LatexParser`: đổi ký tự `−` (`U+2212`) về `-` (`U+002D`) để công thức từ Word dùng dấu trừ chuẩn LaTeX `-`.

3. **Bổ sung các ký hiệu đơn trong 8 bảng Symbols của Word:**
   - Thêm vào `symbols.csv` (hoặc ánh xạ chuẩn):
     - `\iddots,22F0` (`⋰` — Basic Math)
     - `\nequiv,2262` (`≢` — Negated Relations / Operators)
     - `\nsimeq,2244` (`≄`), `\ncong,2247` (`≇`, giữ `2246` làm phụ), `\napprox,2249` (`≉`), `\nasymp,226D` (`≭`), `\ntlg,2278` (`≸`), `\ntgl,2279` (`≹`), `\nsqsubseteq,22E2` (`⋢`), `\nsqsupseteq,22E3` (`⋣`)
     - `\rightangle,221F` (`∟`), `\measuredrightangle,22BE` (`⊾`), `\varlrtriangle,22BF` (`⊿`), `\epar,22D5` (`⋕`), `\ratio,2236` (`∶`), `\qed,220E` (`∎` — Geometry)
     - `\AA,212B` (`Å`), `\Euler,2107` (`ℇ` — Letter-Like)
     - `\dotminus,2238` (`∸` — Advanced Binary Operators)
     - `\dlsh,21B2` (`↵`), `\drsh,21B3` (`↳`), `\leftwavearrow,219C` (`↜`), `\rightwavearrow,219D` (`↝` — Arrows)

### 3.3 Giải mã Unicode Mathematical Alphanumeric Symbols (`MathAlphaDecoder`)

- Tạo lớp nội bộ `src/FormulaConvert.Core/Omml/MathAlphaDecoder.cs`:
  - Hàm `bool TryDecode(Rune rune, out char ascii, out RunStyle style)` tra cứu:
    - Dải `U+1D400..U+1D6A3` và `U+1D7CE..U+1D7FF` (Bold, Italic, BoldItalic, Script/BoldScript, Fraktur/BoldFraktur, DoubleStruck, SansSerif, Monospace cho `A-Z`, `a-z`, `0-9`).
    - Các ký tự BMP Letterlike tương đương thuộc dải `U+2102..U+2134` mà Word dùng cho Scripts, Frakturs, Double-Struck (`ℂ, ℕ, ℙ, ℚ, ℝ, ℤ`, `ℬ, ℰ, ℱ, ℋ, ℐ, ℒ, ℳ, ℛ, ℯ, ℊ, ℴ`, `ℭ, ℌ, ℨ`, `ℎ`), **trừ** các ký hiệu đã có lệnh riêng trong `symbols.csv` (`\hbar` `U+210F`, `\Im` `U+2111`, `\ell` `U+2113`, `\wp` `U+2118`, `\Re` `U+211C`, `\mho` `U+2127`, `\AA` `U+212B`, `\Finv` `U+2132`, `\aleph` `U+2135`..`\daleth` `U+2138`, `\Euler` `U+2107`).
- Trong `OmmlReader.ReadRun` và `LatexParser`:
  - Nếu văn bản không phải `m:nor` (`\text{...}`), duyệt từng `Rune`: nếu `MathAlphaDecoder.TryDecode` khớp (và `m:rPr` không ép kiểu khác), tạo `Run` mang chữ cái ASCII gốc kèm `RunStyle(Sty, Scr)` tương ứng để `OmmlWriter` ghi `<m:rPr>` chuẩn và `LatexWriter` xuất ra `\mathbb{R}`, `\mathcal{A}`, `\mathfrak{g}`, `\mathbf{A}`...
  - Các ký tự ngoài BMP không thuộc bảng chữ cái toán học (ví dụ `𝄞` `U+1D11E`) vẫn đi qua nguyên vẹn (`Non_bmp_passthrough`).

## 4. Kiểm thử

- **`BoxTests` & `FractionTests`:** Kiểm tra đọc `<m:box>` (`dx`, `:=`) và `<m:argPr>` (*Small Fraction*) không phát cảnh báo `unsupported-omml`.
- **`AccentTests`:**
  - Kiểm tra round-trip `\overbrace{x}^n`, `\underbrace{x}_n` với `<m:limUpp>`/`<m:limLow>` bọc `<m:groupChr>`.
  - Cập nhật các ca kiểm thử `\xrightarrow{a}`, `\xrightarrow[b]{}`, `\xrightarrow[b]{a}`, `\xRightarrow{a}` sang cấu trúc `<m:groupChr>` có `m:vertJc` của Word, đồng thời giữ test khẳng định `OmmlToLatex` vẫn đọc được dạng `<m:limUpp>`/`<m:limLow>` cũ.
  - Kiểm tra `\overleftharpoon{x}`, `\overrightharpoon{x}` và đọc `m:acc` `U+033F` (`\overline{\overline{x}}`).
- **`NaryFunctionTests`:** Kiểm tra `\sech`, `\csch`, `\intclockwise`, `\varointclockwise`, `\ointctrclockwise`.
- **`StyleTests` & `CoreTests`:** Kiểm tra cả hai chiều với các ký tự Unicode `ℝ, ℂ, 𝒜, 𝔄, 𝔸, 𝒶, 𝔞, 𝕒, 𝐀` chuyển thành `\mathbb{R}`, `\mathbb{C}`, `\mathcal{A}`, `\mathfrak{A}`, `\mathbf{A}`..., và ký tự ngoài BMP không phải chữ toán học (`𝄞+é中`) đi qua nguyên vẹn.
- **`DataTableTests` & `SymbolSweepTests` & `CatalogTests`:** Khẳng định không còn mã PUA `U+E000..U+F8FF` trong `symbols.csv` và mọi lệnh trong `mathlive-commands.txt` đều được phân loại hợp lệ.
