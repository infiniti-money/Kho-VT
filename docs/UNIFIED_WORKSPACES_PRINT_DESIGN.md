# HISA Kho-VT — Giao diện thống nhất và trình thiết kế mẫu in

## Bố cục sau thiết kế lại
- **Vật tư & tồn kho**: chỉ còn mục **Danh sách vật tư**. Trang gồm hai tab **Danh sách/Tra cứu/Cảnh báo** (có lọc tồn thấp, hết hàng, âm tồn) và **Tem & mã vạch**. Các nút con trùng chức năng được ẩn ở menu, không xóa tính năng.
- **Quản lý phiếu**: Nhập/Xuất kho, Phiếu chờ xác nhận, Yêu cầu vật tư.
- **Đề xuất & mẫu in**: Đề xuất nhập kho, Mẫu in phiếu.
- **Báo cáo & AI**: **Báo cáo & nhật ký** là một trang hai tab Nhật ký và Báo cáo chi tiết, cùng trang AI Gemini.
- **Hệ thống & dữ liệu**: chỉ có trang **Dữ liệu & phần mềm**; có phiên bản phần mềm và thông tin nhà phát triển trong khu vực sao lưu/cập nhật.

**Kho, đơn vị tính, bộ phận:** ẩn trang danh mục chỉnh sửa độc lập theo yêu cầu. Chưa thể tuyên bố tất cả ba danh mục tự đồng bộ từ `KHO.xlsx`: file hiện tại có Mã/Tên/Đơn vị/tồn, nhưng không có đủ cột kho/bộ phận; cần xác định nguồn chính xác trước khi thay đổi dữ liệu gốc. Các danh mục có sẵn vẫn tồn tại trong cache để không làm hỏng phiếu.

## Chuyển trang không cần bấm Đóng
- Chọn trang khác trong menu: nếu trang trước chỉ đọc, chuyển ngay; nếu đang ở biểu mẫu có thể còn nội dung chưa lưu, hỏi xác nhận **Chuyển trang / Ở lại**. Không tạo cửa sổ chồng, không cần tự bấm Đóng.
- HISA giữ xác nhận khi chuyển khỏi cửa sổ nhập phiếu, sửa vật tư hoặc mẫu in để tránh mất dữ liệu chưa lưu.
- Các thao tác đã lưu vẫn ở trong localStorage; không sửa file Excel gốc.

## Trình thiết kế mẫu in
- Trong **Đề xuất & mẫu in → Mẫu in phiếu**, tạo mới/xóa mẫu, sửa đơn vị, tiêu đề, thông tin, chữ ký.
- Căn chỉnh tương tự Excel ở mức **căn trái/giữa/phải cho đầu phiếu, phông chữ, cỡ chữ, lề A4 và đệm ô bảng**.
- Bên phải xem trước phiếu A4 với dữ liệu **minh họa**. Nhấn **Cập nhật bản xem trước** để đồng bộ các thay đổi trước khi lưu.
- Các cài đặt được áp dụng vào phiếu nhập/xuất và phiếu yêu cầu khi in thật (trừ phần dữ liệu minh họa); không thay đổi mẫu gốc Excel.
- Khi xóa một mẫu nghiệp vụ bắt buộc, lúc in phiếu đó hệ thống dùng mẫu mặc định để không làm mất chức năng in.
- Chưa có trình chỉnh từng ô với công thức Excel hoặc kéo thả pixel. Cần xem đây là bước đầu, không phải trình Excel tích hợp.

## Kiểm thử yêu cầu
- Build WPF + kiểm thử tab, menu, chuyển trang.
- Thiết kế mẫu, xem trước, lưu/hủy, tạo/xóa mẫu, kiểm tra lề/phông/cỡ chữ, in phiếu vào PDF Printer.
- Đóng biểu mẫu chưa lưu: chọn Ở lại không mất dữ liệu, chọn Chuyển trang thì bỏ thay đổi chưa lưu.
- Cài lên máy Windows thật và kiểm tra dữ liệu tồn kho, Excel, khóa Gemini không mất sau nâng cấp.

**Chưa bật ghi hai chiều KHO.xlsx.** AI vẫn chỉ đề xuất kèm duyệt. 
