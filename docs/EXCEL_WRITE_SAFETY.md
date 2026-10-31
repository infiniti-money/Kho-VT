# An toàn truy cập file KHO.xlsx

## Trạng thái hiện tại
- `KHO.xlsx` vẫn là file gốc. HISA hiện **chỉ đọc** file gốc để kiểm tra và đối chiếu, **chưa ghi ngược**.
- `WorkbookWriteSafety.Inspect` chỉ kiểm tra **tại thời điểm bấm kiểm tra** xem có điều kiện ghi hay không: có file khóa Excel `~$KHO.xlsx`, file chỉ đọc, file đang bị phần mềm khác mở hoặc lỗi quyền truy cập.
- Khi Excel đang mở, HISA vẫn có thể **đọc bản đã lưu** để xem, không được ghi. Các thay đổi chưa nhấn Lưu trong Microsoft Excel không xuất hiện trong HISA.
- Cảnh báo `Không phát hiện xung đột` **không phải quyền cho phép ghi**. Đây là phép kiểm tra tức thời; không giữ khóa sau khi kiểm tra.
- Không sửa, di chuyển, ghi đè file gốc, không truy cập 5 sheet phụ để can thiệp dữ liệu.

## Bắt buộc trước khi triển khai ghi hai chiều
1. Xác minh chính xác workbook gốc (tên 2 sheet, cột nhập liệu, công thức, vùng in, cấu trúc và các liên kết).
2. Ghi vào **bản sao thử nghiệm** theo từng ô được phép; không ghi vào ô có công thức và không chạm các sheet khác.
3. Trước khi áp dụng thay đổi: kiểm tra `~$`, lấy khóa độc quyền **trong suốt quá trình commit** và so sánh hash/số phiên bản với bản đã đọc; thấy thay đổi từ Excel thì từ chối và yêu cầu đối chiếu.
4. Tạo bản sao lưu nguyên byte của workbook, ở thư mục dữ liệu người dùng nằm ngoài thư mục cài đặt; xác thực bản sao lưu trước khi ghi.
5. So sánh công thức, định dạng, các part ZIP/XML ngoài phạm vi chỉnh sửa; nếu sai, giữ file gốc và báo lỗi. Ưu tiên ghi atomically và có rollback.
6. Kiểm thử trên Windows: Excel đang mở, Excel đang Lưu, Excel vừa đổi dữ liệu, Excel dùng OneDrive/SMB, file chỉ đọc, lỗi ổ đĩa, workbook chứa công thức, mẫu in, liên kết và macro (nếu có).

**Lưu ý:** Bản smoke test CI kiểm tra khóa file và trạng thái, không thay cho kiểm thử thực tế với Microsoft Excel.
