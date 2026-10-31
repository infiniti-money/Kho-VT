# HISA Kho-VT v0.1.3 — Giao diện thống nhất và xem trước mẫu in

Ngày phát hành: 31/10/2026

## Nâng cấp giao diện
- **Vật tư & tồn kho**: một trang có 2 tab: Danh sách/Tra cứu/Cảnh báo và Tem & mã vạch.
- **Quản lý phiếu**: nhập/xuất, phiếu chờ xác nhận và phiếu yêu cầu cấp/nhập vật tư.
- **Đề xuất & mẫu in**: đề xuất nhập và trình quản lý mẫu in, chia nhóm riêng cho thao tác thuận tiện.
- **Báo cáo & AI**: trang **Báo cáo & nhật ký** có 2 tab Nhật ký giao dịch và Báo cáo chi tiết; AI Gemini tiếp tục ở mục riêng.
- **Dữ liệu & phần mềm**: thông tin phiên bản được gộp vào khu vực Sao lưu & cập nhật, không còn mục thông tin riêng trên thanh bên.
- Ẩn các menu con bị trùng và mục chỉnh sửa kho/đơn vị/bộ phận độc lập; dữ liệu cũ **không bị xóa**.

## Chuyển trang
- **Không cần bấm Đóng** để mở trang khác. Trang chỉ đọc được thay ngay khi chọn menu mới.
- Đối với biểu mẫu có thể có nội dung **chưa lưu**, HISA hỏi **Chuyển trang** hoặc **Ở lại**, nhằm tránh mất dữ liệu. Thao tác không mở cửa sổ chồng nhau.
- Dữ liệu phiếu và các chức năng in/AI cũ tiếp tục được giữ lại.

## In phiếu và thiết kế mẫu
- Trình **Mẫu in phiếu** mới: thêm/xóa mẫu, sửa đơn vị, tiêu đề, dòng đầu/cuối, ba ô chữ ký.
- **Căn chỉnh cơ bản tương tự Excel**: căn trái/giữa/phải cho tiêu đề, chọn Arial/Segoe UI/Times New Roman, chỉnh cỡ chữ, lề A4 (mm), đệm ô bảng.
- **Xem trước A4 với dữ liệu minh họa** ngay khi thiết kế.
- Khi in phiếu thật (nhập/xuất/yêu cầu) hoặc tem mã vạch, có **màn hình xem trước nội dung thực tế** rồi mới chọn In. Không tự gửi lệnh in khi chưa xác nhận.
- Mẫu đã chỉnh được lưu trong dữ liệu HISA, áp dụng cho phiếu mới; xóa mẫu nghiệp vụ bắt buộc sẽ dùng lại mẫu mặc định khi cần in để không mất chức năng.

## Tải về
- `HISA-KhoVT-Setup.exe`: bộ cài Windows x64 tiếng Việt, cài đè phiên bản 0.1.0–0.1.2.
- `HISA-KhoVT-Windows-x64.zip`: giải nén toàn bộ trước khi chạy `HISA Quan Ly Kho.exe`.
- `SHA256SUMS.txt`: mã kiểm tra hai bản tải về.

## Bảo toàn dữ liệu & giới hạn
- Sao lưu JSON trước khi cài nâng cấp. Dữ liệu WebView2 trong hồ sơ Windows, khóa Gemini và file `KHO.xlsx` ngoài thư mục cài sẽ không bị bộ cài chủ động thay thế.
- **Chưa đồng bộ ghi hai chiều** vào `KHO.xlsx`, chưa tự khôi phục công thức và chưa cho AI sửa Excel không có phê duyệt.
- Mục chỉnh sửa danh mục kho/đơn vị/bộ phận đã được **ẩn khỏi menu**, nhưng việc lấy **đầy đủ** các danh mục trực tiếp từ Excel chưa hoàn thành: workbook gốc chưa có đủ cấu trúc kho để tạo toàn bộ cấu hình. Không tự xóa danh mục cũ.
- Công cụ căn chỉnh mẫu in hiện chưa có **kéo-thả từng ô hoặc công thức như Excel**; bản này cung cấp thiết lập lề/font/căn chữ và xem trước.
- CI xác minh biên dịch Windows, các kiểm thử an toàn Excel, nhóm điều hướng, cập nhật và đóng gói EXE. **Chưa nghiệm thu việc dùng chuột, màn hình hiển thị và máy in thật** của người dùng.
- EXE chưa ký số, Windows SmartScreen có thể yêu cầu xác nhận.
- Chế độ silent khi phần mềm cài trong Program Files có thể cần quyền UAC; các giới hạn trước đó vẫn giữ nguyên.

Báo lỗi/yêu cầu: https://github.com/infiniti-money/Kho-VT/issues
