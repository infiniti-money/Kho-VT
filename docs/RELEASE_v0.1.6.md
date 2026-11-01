# HISA Kho-VT v0.1.6 — Mở rộng bảng Excel tự động và dùng mẫu Phiếu in gốc

## Mở rộng bảng Nhập Xuất
- Mỗi khi HISA lưu phiếu Nhập/Xuất trực tiếp hoặc xác nhận phiếu chờ, file `KHO.xlsx` chính được tự động nới bảng khi cần; bảo đảm **còn ít nhất 50 dòng trống** sau các giao dịch vừa ghi.
- Mở rộng bảng theo đợt **200 dòng**, hoặc lớn hơn nếu phiếu cần nhiều chỗ.
- Giữ nguyên các cột nhập liệu A/C/D/G/K/L và cột B (chỉ số in); **không chép đè công thức** E/F/H/I/J. Dòng mới tự có định dạng và công thức tương ứng, bao gồm công thức shared cũ chuyển sang công thức tường minh ở dòng mới.
- Mở rộng vùng bảng `tbl_NhapXuat7`, AutoFilter, danh sách chọn Nhập/Xuất và công thức phụ thuộc trong `Danh mục`, `Báo cáo`, `Phiếu in` cùng các công thức Excel Tables. Giữ dữ liệu cũ và các sheet khác không bị tác động nếu không có tham chiếu cần sửa.
- Sao lưu tự động cạnh file chính rồi mới thay thế nguyên tử; chặn ghi khi file đang mở trong Excel, thiếu công thức hoặc bảng bị sai cấu trúc. Excel sẽ tự tính lại công thức khi mở.

## In phiếu nhập/xuất sử dụng chính sheet Phiếu in
- Trong **Báo cáo & nhật ký → Nhật ký giao dịch**, chọn một dòng của phiếu Nhập/Xuất từ HISA và nhấn **In phiếu từ Excel**.
- Phần mềm đọc mẫu **từ chính sheet `Phiếu in`**. Trên **bản sao tạm của file**, HISA chọn các dòng của số phiếu qua mã chống trùng ở cột L, tính toán bằng Microsoft Excel rồi xuất PDF theo đúng định dạng và vùng in của Excel.
- Có **xem trước PDF ngay trong HISA** và nút **In từ bản xem trước**; thao tác này không thay đổi file Excel gốc.
- Thay đổi mẫu nhập/xuất bằng cách sửa sheet `Phiếu in` trong Microsoft Excel. Trình quản lý mẫu nội bộ của HISA vẫn dùng cho phiếu yêu cầu, không thay thế sheet mẫu gốc.

## Giới hạn và kiểm thử
- Phần **xuất PDF theo sheet gốc yêu cầu Microsoft Excel cho Windows** đã cài trên máy sử dụng. Không có Excel thì HISA báo lỗi rõ ràng, không tự tạo mẫu khác.
- Mẫu `Phiếu in` hiện có **20 dòng chi tiết**; phiếu hơn 20 dòng được báo lỗi để tránh in thiếu. Nâng dung lượng mẫu in nhiều trang sẽ là công việc tiếp theo.
- Hai CI Windows thành công khi build nhánh tính năng, gồm kiểm thử tự mở rộng 920 dòng, 50 dòng trống, công thức dùng chung, SUMIFS, phạm vi sheet Phiếu in, bảng/validation/backup/khóa ghi, build và Inno Setup.
- CI không có Microsoft Office hoặc máy in thật: **chưa kiểm thử end-to-end việc Excel xuất PDF và máy in**. Sau khi cài nên thử trên **bản sao** `KHO.xlsx` với Microsoft Excel, xác minh công thức, ngắt Excel khi ghi và xem trước phiếu.
- Chưa tự đồng bộ các sửa đổi thủ công từ Excel về HISA. Chưa khôi phục công thức bị xóa; HISA chặn ghi an toàn.
- Chỉ cho phép Excel tối đa 1.048.576 dòng. Không chèn vượt giới hạn. Bộ cài chưa ký số.

## Nâng cấp
1. **Sao lưu JSON của HISA và KHO.xlsx gốc** trước khi cài.
2. Đóng HISA cũ và Microsoft Excel.
3. Cài `HISA-KhoVT-Setup.exe` 0.1.6.
4. Chọn **bản sao KHO.xlsx** làm file chính ở khu vực Dữ liệu & phần mềm.
5. Nhập thử một phiếu Nhập/Xuất, mở Excel để kiểm tra vùng bảng và các công thức; trở lại HISA, chọn dòng giao dịch rồi nhấn **In phiếu từ Excel**.
6. Chỉ chuyển sang dùng file gốc sau khi đã kiểm thử đủ tính toán, in ấn và bản sao lưu.

Tài liệu chi tiết: `docs/EXCEL_AUTOEXPAND_AND_PRINT_SOURCE.md`.
