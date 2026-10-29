# Độ trung thực OMML ↔ LaTeX (MathLive) — Thiết kế

Ngày: 2026-10-29. Kế thừa `2026-10-28-omml-latex-converter-design.md` và `2026-10-29-word-omml-full-coverage-design.md`.

## 1. Mục tiêu

LaTeX mà `OmmlToLatex` sinh ra phải **hiển thị đúng trong MathLive** (bản 0.111.x), và LaTeX do MathLive sinh ra phải **đọc được trọn vẹn** bởi `LatexToOmml`. Nguồn đối chiếu là mã nguồn MathLive (`src/latex-commands/*.ts`, `src/core/*.ts`, MIT), không phải trang tài liệu: trang tài liệu thiếu `\deg`, `\bmod`, `\pmod`, `\genfrac`, `\mathnormal`, `\mspace`…

Tiêu chí thành công:
- Mọi lệnh và môi trường mà `LatexWriter` có thể xuất đều có trong MathLive (test quét toàn bộ bảng dữ liệu).
- Mọi lệnh mà MathLive định nghĩa đều được phân loại (hỗ trợ, hoặc bỏ vỏ kèm `Diagnostic`); không còn `unknown command` cho đầu vào do MathLive sinh.
- Mọi mẫu trong 21 bảng Equation của Word (ảnh ở `word-equation-images/`) chuyển được sang LaTeX mà MathLive vẽ đúng.

## 2. Phi mục tiêu

- Không thêm lệnh LaTeX mà MathLive không định nghĩa (`\substack`, `\tag`, `\label`, `\prescript`, `\sideset`, `\tensor`, `\cancelto`, `\overbracket`, `alignat`, `subarray`, `CD`…). Chúng vẫn báo `unknown command`. Ngoại lệ duy nhất: các lệnh Task 3 của kế hoạch trước đã thêm (đọc được, nhưng không bao giờ được xuất).
- Không cố biểu diễn cỡ chữ, màu, hay kiểu hiển thị (`\displaystyle`) trong OMML.
- Không có Word để so sánh; thay đổi OMML phải dẫn ECMA-376.

## 3. Quyết định

**D1. Chỉ xuất lệnh có trong MathLive.** `Data/not-in-mathlive.txt` (`command,kind`) liệt kê lệnh vẫn đọc được nhưng không được xuất. `kind`: `rel`/`bin` (xuất `\mathrel{c}`/`\mathbin{c}` quanh ký tự để MathLive đặt khoảng cách đúng), `ord` (ký tự trần), `fn` (xuất `\operatorname{tên}`), `nary` (ký tự trần). Test đối chiếu với `tests/.../Data/mathlive-defined.txt` do `tools/extract_mathlive_defined.py` sinh từ mã nguồn MathLive; lệnh mới không có trong MathLive mà không được liệt kê sẽ làm test đỏ. `\mathrel`/`\mathbin` bao một ký tự duy nhất đọc vào không phát cảnh báo.

**D2. Lệnh chuẩn khi đọc ngược.** Khi nhiều lệnh cùng một codepoint, chọn lệnh phổ thông nhất: `≰ \nleq`, `≱ \ngeq`, `⊈ \nsubseteq`, `⊉ \nsupseteq`, `∤ \nmid`, `∦ \nparallel`, `⪇ \lneq`, `⪈ \gneq`, `≨ \lneqq`, `≩ \gneqq`, `≈ \approx`, `∼ \sim`, `∥ \parallel`, `∣ \mid`, `⟷ \longleftrightarrow`.

**D3. Khoảng trắng là ký tự Unicode trong `m:t`.** `\,`→U+2009, `\:`→U+2005, `\;`→U+2004, `\quad`→U+2003, `\qquad`→U+2003U+2003, `\enspace`→U+2002, `\ ` và `~`→U+00A0. `\!` (khoảng âm) không có ký tự: bỏ kèm `lossy`. Chiều ngược: U+2009/U+2006/U+200A→`\,`; U+2005/U+205F→`\:`; U+2004→`\;`; hai U+2003 liên tiếp→`\qquad`, một→`\quad`; U+2002→`\enspace`; U+00A0 và U+0020→`\ `. Chỉ áp dụng trong run toán, không trong `\text{}` (`m:nor`). `m:t` có khoảng trắng thì ghi `xml:space="preserve"`.

**D4. Phân số.** `FracKind.Skw` mới: `\nicefrac` ↔ `m:type skw`. `FracKind.Lin` (chỉ đọc từ OMML) xuất `{n}/{d}` (bỏ `{}` khi một ký tự) kèm `lossy`, vì MathLive không có phân số một dòng thật sự.

**D5. Kiểu chữ.** `Scr.Script` xuất `\mathscr` (MathLive dịch ký tự `𝒜` thành `\mathscr`); `\mathcal` vẫn đọc được. `MathAlphaDecoder` giải mã thêm chữ Hy Lạp toán học (U+1D6A8..U+1D7CB: đậm, nghiêng, đậm nghiêng).

**D6. Dấu phụ rộng.** `\hat`/`\tilde`/`\check`/`\vec`/`\overleftarrow` có thân nhiều hơn một ký tự xuất `\widehat`/`\widetilde`/`\widecheck`/`\overrightarrow`/`\overleftarrow`. `m:acc` với U+0305 là `\overline`, U+0332 là `\underline`.

**D7. Điểm căn hàng.** Word đánh dấu điểm căn trong `m:eqArr` bằng `<m:aln/>` trong `m:rPr`, không phải ký tự `&`. Reader đổi `m:aln` thành `Run("&")`; writer tách run tại `&` và ghi `m:aln` (chỉ trong `m:eqArr`). `&` nguyên văn trong `m:t` vẫn đọc được. `eqnarray` là `align`. `m:brk` bỏ kèm `lossy`.

**D8. Phantom.** `m:phant` có `show=1` và `zeroWid=1` ↔ `\mathrlap{x}`. Các tổ hợp khác có `show=1` cùng `zeroAsc`/`zeroDesc`/`zeroWid` mất thông tin: phát `lossy`.
