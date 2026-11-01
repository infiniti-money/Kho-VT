# HISA Kho-VT v0.1.5 — Ghi phiếu Nhập/Xuất trực tiếp vào KHO.xlsx

## Tính năng mới
- **Lưu phiếu Nhập/Xuất kho:** mỗi mặt hàng trên phiếu được ghi thành một dòng vào sheet `Nhập Xuất` trong file `KHO.xlsx` đã chọn làm file chính.
- **Phiếu chờ xác nhận:** chỉ ghi vào file khi người dùng bấm **Xác nhận**, không ghi khi lưu ở trạng thái chờ.
- **Chỉ ghi ô nhập tay:** A Ngày; C Loại; D Mã hàng; G Số lượng; K Giao/nhận; L Ghi chú và mã chống ghi trùng. **B (In)** giữ nguyên. **E, F, H, I, J** chỉ có công thức nên không thay đổi.
- Giữ nguyên các công thức trên sheet `Danh mục` và 5 sheet không được phép chỉnh sửa khác, cùng các phần biểu đồ, mẫu phiếu in, liên kết ngoài, định dạng và vùng M:Q.
- Khi mở Excel sau khi ghi, Excel tự tính lại các công thức từ giá trị mới; HISA không ghi cứng tồn trước/tồn sau.
- **Không ghi ra ngoài dòng 965** do công thức tồn kho hiện giới hạn phạm vi 965. Không tự mở rộng bảng hoặc sửa công thức.
- **Sao lưu tự động:** HISA tạo file `KHO.xlsx.HISA-backup-*.bak` ngay cạnh file gốc trước khi thay thế thành công, dùng cơ chế thay thế nguyên tử.
- Khóa tạm của Excel, file chỉ đọc, công thức bị thiếu, SKU không có trong Danh mục, mã phiếu đã ghi trước đó, thiếu file chính hoặc dòng đã hết => báo lỗi và **không cập nhật tồn kho HISA**.

## Cách sử dụng
1. Sao lưu file `KHO.xlsx` và dữ liệu HISA (xuất JSON) trước khi nâng cấp.
2. Đóng Microsoft Excel đang mở file `KHO.xlsx`.
3. Mở **Dữ liệu & phần mềm**, chọn đường dẫn file **KHO.xlsx chính** và kiểm tra cấu trúc.
4. Tạo phiếu **Nhập & Xuất kho** trong HISA; bấm lưu phiếu ngay hoặc lưu chờ và xác nhận sau. Mỗi dòng mới sẽ được nối sau dòng giao dịch cuối trong vùng từ 5 đến 965.
5. Mở lại `KHO.xlsx` bằng Excel để xem công thức tự tính lại và lưu workbook.
6. Nếu có lỗi, kiểm tra hộp thoại và backup cạnh file, **không lập lại phiếu cũ tùy tiện**.

## Giới hạn cần biết
- Chức năng v0.1.5 là **ghi một chiều từ phiếu HISA sang Excel chính**, chưa tự đồng bộ liên tục những thay đổi nhập thủ công từ Excel về HISA.
- **Điều chuyển nội bộ** không ghi xuống sheet `Nhập Xuất` vì file gốc không có cột kho và chỉ chấp nhận hai loại `Nhập`/`Xuất`. Điều chuyển không làm thay đổi tổng tồn toàn file.
- Nếu file Excel vừa ghi xong nhưng phần lưu trạng thái HISA bị lỗi, HISA báo cần đối chiếu; mã phiếu được lưu trong cột L để phát hiện trùng.
- Chức năng **khôi phục công thức bị xóa** chưa triển khai; khi thiếu công thức HISA từ chối ghi, không tự dựng công thức.
- File còn dưới 30 dòng giao dịch trống ở vùng bảng hiện có. Khi đầy cần phê duyệt kế hoạch mở rộng công thức an toàn, không được tự ghi dòng 966.
- Chưa chạy kiểm thử thao tác trong Microsoft Excel trên bản sao file thật bằng máy Windows của người dùng; CI dùng workbook thử nghiệm có cấu trúc tương ứng và xác nhận WPF build, công thức, backup, Excel lock, duplicate, giới hạn 965, ZIP và installer.
- Bộ cài chưa ký số. Cập nhật silent trong Program Files vẫn có thể cần quyền UAC.

## Tải về
- `HISA-KhoVT-Setup.exe` — bộ cài tiếng Việt Windows x64, cài đè bản cũ.
- `HISA-KhoVT-Windows-x64.zip` — bản portable.
- `SHA256SUMS.txt` — mã kiểm tra tệp.

Hướng dẫn chi tiết: `docs/WRITE_PRIMARY_EXCEL_TRANSACTIONS.md`.
