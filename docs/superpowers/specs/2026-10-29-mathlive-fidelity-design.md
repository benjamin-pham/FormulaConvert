# Độ trung thực OMML ↔ LaTeX (MathLive) — Thiết kế

Ngày: 2026-10-29. Kế thừa `2026-10-28-omml-latex-converter-design.md` và `2026-10-29-word-omml-full-coverage-design.md`.

## 1. Mục tiêu

LaTeX mà `OmmlToLatex` sinh ra phải **hiển thị đúng trong MathLive 0.111.1** (bản ghi trong `THIRD-PARTY-NOTICES.md`), và LaTeX do MathLive sinh ra phải **đọc được trọn vẹn** bởi `LatexToOmml`.

Nguồn đối chiếu duy nhất là `package/mathlive.mjs` trong gói npm `mathlive@0.111.1`, cũng là file `tools/extract_symbols.py` đang đọc. Không dùng nhánh master trên GitHub, vì không có tag 0.111.x và master khác bản npm. Không dùng trang tài liệu, vì nó thiếu `\deg`, `\bmod`, `\genfrac`, `\mspace`… Khối `INLINE_SHORTCUTS` của bundle là phím tắt khi gõ, không phải lệnh LaTeX.

Tiêu chí thành công:
- Mọi lệnh và môi trường mà `LatexWriter` có thể xuất đều có trong MathLive (test quét toàn bộ bảng dữ liệu).
- Mọi lệnh mà MathLive định nghĩa đều được phân loại (hỗ trợ, hoặc bỏ vỏ kèm `Diagnostic`); đầu vào do MathLive sinh không còn gặp `unknown command`.
- Mọi mẫu trong 21 bảng Equation của Word (ảnh chụp của người dùng, không commit) chuyển được sang LaTeX mà MathLive vẽ đúng. Danh sách ca test nằm trong plan.

## 2. Phi mục tiêu và giới hạn

- Không thêm lệnh LaTeX mà MathLive không định nghĩa (`\dots`, `\substack`, `\tag`, `\label`, `\prescript`, `\sideset`, `\tensor`, `\cancelto`, `\overbracket`, `alignat`, `subarray`, `CD`…); chúng vẫn báo `unknown command`. Ngoại lệ: các lệnh kế hoạch trước đã thêm vẫn đọc được, nhưng không bao giờ được xuất (D1).
- Ký tự không có lệnh LaTeX nào (ví dụ `≙`, `⨅`) được ghi nguyên văn. MathLive coi chúng là ký hiệu thường, nên khoảng cách quanh chúng có thể khác Word.
- Không biểu diễn cỡ chữ, màu, hay kiểu hiển thị (`\displaystyle`) trong OMML.
- Bảng Large Operator của Word chưa có ảnh; chỉ kiểm các toán tử lớn xuất hiện ở bảng Operators.
- Không có Word để so sánh; thay đổi OMML phải dẫn ECMA-376.

## 3. Quyết định

**D1. Chỉ xuất lệnh có trong MathLive.**
- `Data/not-in-mathlive.txt` (`command,kind`) liệt kê lệnh vẫn đọc được nhưng không được xuất. Các `kind`:
  - `rel`: xuất `\mathrel{c}`;
  - `bin`: xuất `\mathbin{c}`;
  - `ord`: ghi ký tự trần;
  - `fn`: xuất `\operatorname{tên}`, đúng cách chính MathLive viết `sech`;
  - `nary`: ghi ký tự trần.
- Test đối chiếu với danh sách do `tools/extract_mathlive_defined.py` sinh từ bundle. Một lệnh mới không có trong MathLive mà chưa được liệt kê sẽ làm test đỏ.
- Khi đọc vào, `\mathrel` hoặc `\mathbin` bao **đúng một ký tự** không phát cảnh báo. Mọi trường hợp khác, và các lệnh `\mathord`/`\mathop`/…, vẫn phát `out-of-scope` như cũ.

**D2. Lệnh chuẩn khi đọc ngược.** Khi nhiều lệnh cùng một codepoint, chọn lệnh phổ thông nhất:

| Ký tự | Lệnh | Ký tự | Lệnh | Ký tự | Lệnh |
|---|---|---|---|---|---|
| `≰` | `\nleq` | `∤` | `\nmid` | `≈` | `\approx` |
| `≱` | `\ngeq` | `∦` | `\nparallel` | `∼` | `\sim` |
| `⊈` | `\nsubseteq` | `⪇` | `\lneq` | `∥` | `\parallel` |
| `⊉` | `\nsupseteq` | `⪈` | `\gneq` | `∣` | `\mid` |
| | | | | `⟷` | `\longleftrightarrow` |

**D3. Khoảng trắng là ký tự Unicode trong `m:t`.**

| Lệnh LaTeX | Ký tự trong OMML |
|---|---|
| `\,` | U+2009 |
| `\:`, `\>` | U+2005 |
| `\;` | U+2004 |
| `\quad` | U+2003 |
| `\qquad` | U+2003 U+2003 |
| `\enspace`, `\enskip` | U+2002 |
| `\ `, `~`, `\space`, `\nobreakspace` | U+00A0 |
| `\!` (khoảng âm) | không có; bỏ kèm `lossy` |

Chiều ngược:

| Ký tự trong OMML | Lệnh LaTeX |
|---|---|
| U+2009, U+2006, U+200A | `\,` |
| U+2005, U+205F | `\:` |
| U+2004 | `\;` |
| hai U+2003 liên tiếp | `\qquad` |
| một U+2003 | `\quad` |
| U+2002 | `\enspace` |
| U+00A0, U+0020 | `\ ` |

Phạm vi áp dụng:
- Chỉ trong run toán. `\text{}` (`m:nor`) giữ nguyên ký tự.
- Khoảng trắng trong tên hàm (`\operatorname{arg\,max}`) vẫn bị bỏ như hiện nay.
- `xml:space="preserve"` theo quy tắc hiện có: ký tự đầu hoặc cuối là khoảng trắng (`char.IsWhiteSpace`, đúng với mọi ký tự trên).

**D4. Phân số.**
- `FracKind.Skw` mới: `\nicefrac` ↔ `m:type skw`.
- `FracKind.Lin` (chỉ đọc từ OMML) xuất `n/d`, kèm `lossy` khi đọc. Mỗi vế được bọc `{}` trừ khi nó là một `Run` đúng một ký tự.
- Một phân số `Lin` không phải một atom: làm cơ số chỉ số hay toán hạng thì cả cụm được bọc `{}` (`{a/b}^2`).

**D5. Kiểu chữ.**
- `Scr.Script` xuất `\mathscr`, vì MathLive dịch ký tự `𝒜` thành `\mathscr`; `\mathcal` vẫn đọc được.
- `MathAlphaDecoder` giải mã thêm chữ Hy Lạp toán học (U+1D6A8..U+1D7CB: đậm, nghiêng, đậm nghiêng). Vị trí ϴ của mỗi khối ánh xạ về U+03F4, không phải U+03A2 (codepoint chưa gán).

**D6. Dấu phụ và dấu phẩy trên.**
- `\hat`/`\tilde`/`\check`/`\vec` có thân nhiều hơn một ký tự xuất `\widehat`/`\widetilde`/`\widecheck`/`\overrightarrow`. U+20D6 nhiều ký tự xuất `\overleftarrow`.
- `m:acc` với U+0305 là `\overline`, với U+0332 là `\underline`.
- Ký tự `′`/`″`/`‴` đứng ngay sau một ký tự trong cùng run toán được đọc thành chỉ số trên của ký tự đó, tức `f^{\prime}`. MathLive vẽ `f\prime` thành dấu phẩy to sai vị trí.

**D7. Điểm căn hàng.**
- Word đánh dấu điểm căn trong `m:eqArr` bằng `<m:aln/>` trong `m:rPr` (ECMA-376 §22.1.2.3), không phải ký tự `&`.
- Reader đổi `m:aln` thành `Run("&")`, kể cả khi `m:t` rỗng, nhưng chỉ với run là con trực tiếp của `m:e` trong `m:eqArr` (hoặc nằm trong vỏ `w:` trong suốt). `m:aln` ở nơi khác bị bỏ qua.
- Writer tách run tại `&` và ghi `m:aln`. `&` nguyên văn trong `m:t` vẫn đọc được.
- `eqnarray` là `align`.
- `m:brk` bị bỏ kèm `lossy`.

**D8. Phantom.** Ánh xạ đầy đủ (`show`, `zeroWid`, `zeroAsc`, `zeroDesc`):

| Hiện | Rộng 0 | Cao 0 | Sâu 0 | LaTeX |
|---|---|---|---|---|
| tắt | | | | `\phantom` |
| tắt | | bật | bật | `\hphantom` |
| tắt | bật | | | `\vphantom` |
| bật | | | | nội dung |
| bật | | bật | bật | `\smash` |
| bật | bật | | | `\mathrlap` |

`\rlap` đọc như `\mathrlap`. Tổ hợp khác: nếu tắt hiện thì ghi `\phantom`, nếu bật hiện thì ghi nội dung, cả hai kèm `lossy`.

**D9. Macro của MathLive.** Chép định nghĩa macro của MathLive làm alias, nhưng không dùng các vỏ sẽ phát `out-of-scope`:

| Lệnh | Định nghĩa alias |
|---|---|
| `\bmod` | `\;\mathrm{mod}\;` |
| `\pmod{n}` | `\quad(\mathrm{mod}\ n)` |
| `\mod{n}` | `\quad\mathrm{mod}\,\,n` |
| `\bra{x}` | `\langle{x}|` |
| `\ket{x}` | `|{x}\rangle` |
| `\braket{x}` | `\langle{x}\rangle` |
| `\Bra{x}` | `\left\langle x\right|` |
| `\Ket{x}` | `\left|x\right\rangle` |
| `\Braket{x}` | `\left\langle x\right\rangle` |
