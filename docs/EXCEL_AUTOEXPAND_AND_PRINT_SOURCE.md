# Tự mở rộng bảng Nhập Xuất và in theo sheet Phiếu in

## Khi lưu hoặc xác nhận phiếu Nhập/Xuất

- HISA tiếp tục chỉ ghi vào **ô nhập liệu** `A/C/D/G/K/L`. Không đè công thức `E/F/H/I/J`; giữ `B` (chỉ số chọn phiếu in) nguyên trạng.
- Kiểm tra số dòng từ `tbl_NhapXuat7` và sheet `Nhập Xuất`; nếu không đồng nhất, dừng ghi.
- Tính số dòng đã dùng, số dòng cần ghi và **dự trữ tối thiểu 50 dòng chưa nhập liệu sau dòng cuối**.
- Khi sắp hết chỗ, HISA **thêm một đợt 200 dòng** (hoặc nhiều hơn nếu số dòng giao dịch lớn hơn). Mỗi dòng mới sao chép **định dạng và công thức tính tự động**, riêng phần công thức dùng chung được chuyển thành công thức tường minh để không đụng vùng công thức shared cũ.
- **Tự cập nhật** phạm vi bảng `tbl_NhapXuat7`, AutoFilter, danh sách chọn Nhập/Xuất, và các công thức tham chiếu phạm vi cũ trong sheet `Danh mục`, `Báo cáo`, `Phiếu in` và công thức cột của các Excel Tables. Nhờ đó hàng mới vẫn được SUMIFS, XLOOKUP và mẫu phiếu tính tới.
- Luôn yêu cầu Excel tính lại kết quả công thức khi mở workbook.
- Trước khi ghi vào file gốc: bảo đảm Excel đã đóng, kiểm tra quyền ghi và hash; tạo file mới tạm, bảo toàn các thành phần không liên quan, thay file **nguyên tử** và giữ **backup** bên cạnh.
- Các dữ liệu đã nhập không bị xóa, không nhân đôi phiếu khi bấm lại. Giao dịch chỉ cập nhật tồn HISA sau khi Excel ghi thành công.
- Có giới hạn vật lý **1.048.576 dòng** của Excel; vượt giới hạn thì cảnh báo và dừng an toàn.

## In theo mẫu gốc trong KHO.xlsx

- Khi đang ở **Báo cáo & nhật ký → Nhật ký giao dịch**, chọn một dòng của phiếu Nhập/Xuất do HISA tạo; bấm **In phiếu từ Excel**.
- HISA đọc file `KHO.xlsx`, tạo bản sao tại thư mục tạm, mở bằng Microsoft Excel trong chế độ ẩn, xác định các dòng thuộc số phiếu nhờ `[HISA:<số phiếu>]` ở cột L.
- **Chỉ trên bản sao tạm**, đánh dấu cột B của những dòng này và đặt ô `Phiếu in!C4` để mẫu Excel sẵn có tự điền số liệu. Excel tính lại công thức rồi xuất **đúng vùng in, định dạng, viền, ô gộp và khổ giấy của sheet Phiếu in** ra PDF.
- HISA hiển thị PDF qua WebView2, có thể xem trước và chọn lệnh in Windows. **Xem/in không sửa file `KHO.xlsx` chính.**
- Điều kiện: máy người dùng cần có **Microsoft Excel cho Windows**, WebView2 và máy in tương thích. Nếu chưa có Excel, phần mềm báo yêu cầu cài Excel chứ không âm thầm dùng một mẫu khác.
- Mẫu gốc có 20 dòng chi tiết (8–27), nên hiện giới hạn một lượt in tối đa 20 dòng. Khi nhiều hơn, HISA **cảnh báo thay vì bỏ qua các dòng cuối**. Mở rộng mẫu Phiếu in nhiều trang là giai đoạn tiếp theo.
- Mẫu Nhập/Xuất trong Excel được chỉnh bằng **Microsoft Excel**. Trình thiết kế mẫu in HISA hiện vẫn áp dụng cho **phiếu yêu cầu**, không ghi đè thiết kế sheet Phiếu in.

## Kiểm thử

- CI Windows: WPF build, mở rộng mẫu giả lập 920 dòng, còn tối thiểu 50 dòng trống, công thức shared, dòng đầu/cuối, SUMIFS và mẫu Phiếu in được cập nhật, table, validation, backup, chống ghi trùng, khóa Excel, bảo vệ công thức.
- Build tự động **không chạy Microsoft Excel có giao diện**. Cần thử tạo PDF bằng Excel và kiểm tra trang/giấy/mẫu in trên một **bản sao** workbook thật; không đăng file kho của người dùng lên GitHub công khai.
- Vẫn chưa hoàn thiện hai chiều khi người dùng chỉnh trực tiếp dữ liệu Excel về HISA hoặc tự động khôi phục công thức lỗi; file thiếu công thức sẽ bị chặn ghi.
