# Bộ chuyển OMML ↔ LaTeX (MathLive) bằng C# — Thiết kế

Ngày: 2026-10-28

## 1. Mục tiêu

Một ứng dụng console .NET chuyển hai chiều giữa **OMML** (công thức Office/Word) và **LaTeX theo cú pháp MathLive**.

Kịch bản sử dụng: người dùng soạn công thức bằng MathLive trên web; backend C# ghi công thức vào Word (OMML) và đọc ngược về LaTeX để sửa lại trên MathLive.

Tiêu chí thành công:
- Mọi lệnh trong tài liệu https://mathlive.io/mathfield/reference/commands/ đều được phân loại rõ (Symbol / Structural / OutOfScope) và có test.
- LaTeX → OMML → LaTeX cho kết quả **tương đương sau chuẩn hóa** (không yêu cầu giống từng ký tự).
- OMML do bộ chuyển sinh ra mở được trong Word.

## 2. Phạm vi

Chỉ làm phần **giao** giữa OMML và MathLive.

**Trong phạm vi**

| Nhóm | LaTeX (MathLive) | OMML |
|---|---|---|
| Phân số | `\frac`, `\dfrac`, `\tfrac`, `\cfrac`, `\over`, `\binom`, `\dbinom`, `\tbinom`, `\atop`, `\choose` | `m:f` (`fPr/type`: `bar`, `noBar`) |
| Căn | `\sqrt`, `\sqrt[n]{}` | `m:rad` (`degHide` khi không có bậc) |
| Chỉ số | `^`, `_`, `x_a^b`, `{}_a^b x` | `m:sSup`, `m:sSub`, `m:sSubSup`, `m:sPre` |
| Toán tử lớn | `\sum`, `\prod`, `\coprod`, `\int`, `\iint`, `\iiint`, `\oint`, `\bigcup`… + `\limits`/`\nolimits` | `m:nary` (`chr`, `limLoc`, `subHide`, `supHide`) |
| Ngoặc | `\left…\middle…\right`, ngoặc cỡ cố định | `m:d` (`begChr`, `sepChr`, `endChr`) |
| Ma trận, môi trường | `array`, `matrix`, `pmatrix`, `bmatrix`, `Bmatrix`, `vmatrix`, `Vmatrix`, `smallmatrix`, `cases`, `dcases`, `rcases`, `align`, `aligned`, `gather`, `gathered`, `split`, `multline`, `equation`, `math`, `displaymath` | `m:m`, `m:d`, `m:eqArr` |
| Dấu phụ | `\hat`, `\vec`, `\dot`, `\ddot`, `\bar`, `\tilde`, `\check`, `\breve`, `\mathring`, `\widehat`, `\widetilde`… | `m:acc` |
| Gạch trên/dưới | `\overline`, `\underline` | `m:bar` (`pos`) |
| Ngoặc trên/dưới | `\overbrace`, `\underbrace`, `\overparen`, `\underparen` | `m:groupChr` |
| Đặt trên/dưới | `\overset`, `\underset`, `\stackrel`, `\xrightarrow`… | `m:limUpp`, `m:limLow` |
| Hàm | `\sin`, `\log`, `\lim`, `\max`, `\operatorname{}`… | `m:func` (`fName`, `e`) |
| Khung | `\boxed`, `\cancel`, `\bcancel`, `\xcancel`, `\enclose` (phần tương ứng) | `m:borderBox` |
| Phantom | `\phantom`, `\hphantom`, `\vphantom`, `\smash` | `m:phant` |
| Kiểu chữ | `\mathbf`, `\mathit`, `\mathbfit`, `\mathrm`, `\mathbb`, `\mathcal`, `\mathscr`, `\mathfrak`, `\mathsf`, `\mathtt`, `\text`, `\textrm` | `m:rPr` (`m:sty`, `m:scr`, `m:nor`) |
| Ký hiệu đơn | khoảng 720 lệnh (Hy Lạp, quan hệ, mũi tên, tập hợp, toán tử, logic…) | `m:t` chứa ký tự Unicode |

**Ngoài phạm vi** (OMML không biểu diễn): màu (`\textcolor`, `\colorbox`), cỡ chữ (`\Large`…), `\href`, `\class`, `\cssId`, `\htmlData`, `\texttip`, `\mathtip`, `\error`, `\ce{}` (mhchem), khoảng trắng (`\quad`, `\hspace`…), `\displaystyle` và các lệnh style, họ font (`\fontfamily`…), macro/primitive/register của TeX.

**Quy ước cho lệnh ngoài phạm vi:** bỏ lệnh bọc ngoài, **giữ nội dung bên trong**, ghi một `Diagnostic` (mã, nội dung, vị trí) và in ra stderr. Ví dụ `\textcolor{red}{x+1}` → `x+1` kèm cảnh báo.

**Chưa làm trong bản đầu:** đọc/ghi trực tiếp file `.docx`. Đầu vào/ra chỉ là XML OMML và chuỗi LaTeX. Có thể thêm sau bằng cách giải nén `word/document.xml` và lấy các nút `m:oMath`.

## 3. Kiến trúc

Hướng chọn: **cây trung gian (AST)** dùng chung cho hai chiều.

```
OMML ──OmmlReader──►  AST  ──LatexWriter──► LaTeX
LaTeX ─LatexParser──► AST  ──OmmlWriter───► OMML
```

Cấu trúc dự án (.NET 10):

```
src/FormulaConvert.Core/
  Ast/        FormulaNode và các lớp con
  Omml/       OmmlReader, OmmlWriter (System.Xml.Linq)
  Latex/      LatexTokenizer, LatexParser, LatexWriter
  Data/       symbols.csv, catalog (dữ liệu nhúng)
  Diagnostics Diagnostic, ParseError
src/FormulaConvert.Cli/       console app
tests/FormulaConvert.Tests/   xUnit
```

`Core` không biết gì về console. Không có phụ thuộc ngoài ngoài xUnit cho test.

### 3.1 Mô hình cây

`FormulaNode` và các lớp con: `FormulaRow`, `Run` (văn bản, kiểu `Sty`/`Scr`/`Nor`), `Frac`, `Radical`, `Script` (trên/dưới/cả hai/tiền), `Nary`, `Delimited` (ký tự mở, đóng, ngăn và nhiều phần tử), `Matrix`, `EqArray`, `Accent`, `Bar`, `GroupChar`, `OverUnder`, `FunctionApply`, `BorderBox`, `Phantom`.

Mỗi nút lưu đủ thông tin cho cả hai định dạng (ví dụ `Delimited` lưu ký tự mở/đóng/ngăn, không lưu chuỗi `\left(`).

## 4. Dữ liệu ký hiệu

- Nguồn: mã nguồn MathLive (giấy phép **MIT**, đã xác nhận trong gói npm `mathlive` 0.111.1). Phải ghi chú nguồn và giữ thông báo bản quyền khi dùng dữ liệu này.
- Kết quả khảo sát: 482 trong 721 lệnh đơn của tài liệu có dòng `["\\lệnh", mã Unicode]`. 239 lệnh còn lại cần bảng riêng:

| Loại | Ví dụ | Xử lý |
|---|---|---|
| Hàm (khoảng 52) | `\sin`, `\lim`, `\gcd` | tên hàm → `m:func`/`m:fName` |
| Toán tử lớn (khoảng 33) | `\sum`, `\int`, `\bigcup` | ký tự → `m:nary/m:chr` |
| Accent | `\hat`, `\vec`, `\dot`, `\tilde` | ký tự → `m:acc/m:chr` |
| Bí danh/macro | `\ne`, `\neq`, `\R`, `\N`, `\iff`, `\Alpha` | mở rộng về lệnh gốc |
| Khoảng trắng, font | `\quad`, `\upshape`, `\sffamily` | ngoài phạm vi |

- Bảng được chép vào repo thành file CSV nhúng, không tải lúc chạy.
- **Catalog:** file phân loại **từng lệnh trong tài liệu** thành Symbol, Structural hoặc OutOfScope. Một test khẳng định mọi lệnh trong tài liệu thuộc đúng một nhóm và có xử lý tương ứng.
- Chiều OMML → LaTeX: mỗi ký tự Unicode ứng với một lệnh chuẩn (ví dụ `\le`/`\leq` → `\leq`, `\to`/`\rightarrow` → `\rightarrow`), chọn theo thứ tự định nghĩa của MathLive. Kết quả round-trip vì thế là dạng chuẩn hóa.

## 5. Bộ phân tích LaTeX

1. **Tokenizer:** lệnh (`\name` và lệnh một ký tự), `{ }`, `^`, `_`, `&`, `\\`, `'`, chú thích `%`, khoảng trắng.
2. **Parser đệ quy**, dựa trên bảng đặc tả mỗi lệnh (tên, số đối số bắt buộc, đối số tùy chọn `[ ]`). Ví dụ `\sqrt` có 1 tùy chọn và 1 bắt buộc, `\cfrac` có `[l|r]` và 2 đối số.
3. Điểm khó:
   - `\over`, `\atop`, `\choose` là infix: `a \over b` tác động lên toàn bộ nhóm bao quanh.
   - `\left…\middle…\right` gộp thành một `Delimited` nhiều phần, ngăn bởi ký tự `\middle`.
   - `f'` → `f^{\prime}`.
   - `\limits`/`\nolimits` quyết định `m:limLoc`.
   - Môi trường: hàng cách nhau `\\`, cột cách nhau `&`; `array{lc}` đọc thêm định dạng cột.
   - Macro/bí danh mở rộng trước khi dựng AST.
4. Lỗi cú pháp (thiếu `}`, `\end` không khớp, lệnh lạ) → `ParseError` kèm vị trí; CLI thoát với mã 1.

## 6. Bộ đọc/ghi OMML

Dùng `System.Xml.Linq`. Ánh xạ theo bảng ở mục 2, đối chiếu ECMA-376. Quy ước:
- Các ký tự liên tiếp cùng kiểu gộp vào một `m:r`.
- Writer dùng namespace `m` (`http://schemas.openxmlformats.org/officeDocument/2006/math`).
- Reader chấp nhận cả đoạn `<m:oMath>` lẫn tài liệu có namespace đầy đủ, và `m:oMathPara`.

## 7. Giới hạn đã biết

- `\dfrac`/`\tfrac` đọc được, nhưng OMML không phân biệt với `\frac`; chiều ngược lại chỉ ra `\frac` (kèm cảnh báo).
- Các phần tử có nhiều cách biểu diễn (ví dụ `\xrightarrow`: `m:limUpp` hay `m:groupChr`) có thể khác với cách Word sinh ra. **Chưa có Word để đối chiếu.** Rủi ro này được giảm bằng fixture `.docx` thật khi người dùng cung cấp.
- Không thể bảo toàn thông tin ngoài phạm vi (mục 2) qua round-trip.

## 8. Giao diện dòng lệnh

```
formulaconvert omml2latex [file]    # đọc stdin nếu không có file
formulaconvert latex2omml [file]
```

Kết quả ra stdout, cảnh báo (`Diagnostic`) ra stderr. Mã thoát: 0 thành công (kể cả có cảnh báo), 1 lỗi cú pháp/đầu vào.

## 9. Kiểm thử

- **Test theo bảng:** mỗi lệnh trong catalog có ít nhất một ca LaTeX → OMML và OMML → LaTeX.
- **Round-trip chuẩn hóa:** `LaTeX → OMML → LaTeX` bằng nhau sau khi chuẩn hóa.
- **Test catalog:** mọi lệnh của tài liệu MathLive thuộc đúng một nhóm.
- **Fixture OMML:** viết tay theo ECMA-376; thay/bổ sung bằng mẫu Word thật khi có.
- **Test cảnh báo:** lệnh ngoài phạm vi giữ nội dung và phát `Diagnostic`.

## 10. Các điều chưa chốt

- Người dùng chưa cung cấp file `.docx` mẫu; dùng fixture viết tay đến khi có.
- Hỗ trợ `.docx` trực tiếp để sau.
