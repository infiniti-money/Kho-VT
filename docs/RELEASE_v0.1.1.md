# HISA Kho-VT v0.1.1 — Bản vá lỗi điều hướng (Windows)

Ngày phát hành: 31/10/2026

## Sửa lỗi
- **Chỉ có một trang thao tác hiển thị cùng lúc.** Khi một trang đang mở, bấm menu khác không mở tiếp/chồng lên trang đang dùng. Phần mềm nhắc **đóng trang đang mở trước khi chuyển**.
- Tất cả lối vào chính (Nhập/Xuất, Danh mục, Phiếu chờ, Báo cáo, AI, Đối chiếu, Đề xuất nhập, Quản lý đơn vị…) đều dùng quy tắc thống nhất.
- Biểu mẫu con **Yêu cầu vật tư → Lập phiếu yêu cầu** thay thế vùng hiển thị cha, đóng xong trở lại đúng danh sách mà không tạo lớp trang mới.
- Khi nhấn liên kết từ **AI → Đề xuất nhập kho**, HISA đóng trang AI và giải phóng micro/âm thanh trước khi mở trang mới.
- Trang chính phía sau cửa sổ đang thao tác được ẩn, tránh tương tác nhầm.
- Thêm 6 ca kiểm thử tự động về quy tắc điều hướng và ghép vào CI Windows.

## Tải phần mềm
- `HISA-KhoVT-Setup.exe`: bộ cài tiếng Việt cho Windows x64, nâng cấp cài đặt v0.1.0 có sẵn.
- `HISA-KhoVT-Windows-x64.zip`: ứng dụng portable, giải nén toàn bộ.
- `SHA256SUMS.txt`: mã xác minh tệp.

## Bảo toàn dữ liệu
- Bộ cài dùng AppId và đường dẫn cài đặt cũ. Trước khi nâng cấp nên xuất bản sao lưu JSON.
- HISA **không sửa file `KHO.xlsx` gốc**, không xóa hồ sơ WebView2 hoặc khóa Gemini trong `%LOCALAPPDATA%`.
- Mọi chức năng đã có ở v0.1.0 vẫn giữ nguyên, gồm AI, phiếu yêu cầu, đề xuất nhập, in phiếu, kiểm tra file Excel và cập nhật qua Internet.

## Giới hạn cần tiếp tục hoàn thiện
- Đồng bộ ghi hai chiều vào file `KHO.xlsx`, khôi phục công thức Excel và AI tự chỉnh sửa file kèm duyệt **chưa hoàn thành**.
- Cập nhật im lặng trong `Program Files` vẫn cần đáp ứng quyền UAC; với cài đặt này HISA có thể phải cập nhật thủ công.
- Chưa nghiệm thu thực tế giao diện, micro, in ấn và Gemini trên máy Windows của người dùng; CI xác nhận biên dịch và smoke tests, không thay thế việc dùng thử.
- Tệp cài chưa ký số.
