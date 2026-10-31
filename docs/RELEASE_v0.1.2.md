# HISA Kho-VT v0.1.2 — Gộp các trang cùng chức năng

## Giao diện điều hướng mới
Gộp 15 trang thao tác vào 4 **nhóm nghiệp vụ mở rộng/thu gọn** ở thanh bên:
- **Vật tư & tồn kho:** Danh sách vật tư, tra cứu, cảnh báo tồn, tem mã vạch, kho/đơn vị/bộ phận.
- **Phiếu & nhập xuất:** Phiếu nhập xuất, phiếu chờ, yêu cầu vật tư, đề xuất nhập, mẫu in.
- **Báo cáo & trợ lý AI:** Nhật ký giao dịch, báo cáo chi tiết, trò chuyện Gemini.
- **Hệ thống & dữ liệu:** Sao lưu, quản lý nguồn Excel, cập nhật qua GitHub Releases và thông tin phần mềm.

Tổng quan vẫn là lối tắt riêng. Một lần chỉ mở rộng một nhóm, mục đang hoạt động được làm nổi bật. Các màn hình chức năng gốc vẫn hoạt động và **không chuyển dữ liệu sang một định dạng mới**.

## Không mở chồng trang
Kế thừa bản vá v0.1.1: khi đang mở một phiếu hay biểu mẫu, người dùng phải đóng biểu mẫu hiện tại trước khi chuyển sang trang khác. Biểu mẫu con Lập phiếu yêu cầu vẫn hoạt động an toàn và quay lại danh sách khi đóng.

## Tải và nâng cấp
- `HISA-KhoVT-Setup.exe`: bộ cài Windows x64 tiếng Việt, cài đè lên phiên bản v0.1.0/v0.1.1.
- `HISA-KhoVT-Windows-x64.zip`: phiên bản giải nén, cần giữ toàn bộ thư mục đi kèm ứng dụng.
- `SHA256SUMS.txt`: dùng xác nhận tệp tải về.
- Dữ liệu người dùng, file `KHO.xlsx` gốc và khóa API Gemini trong thư mục hồ sơ Windows được giữ nguyên. **Sao lưu JSON trước khi nâng cấp**.

## Kiểm thử
- Build WPF Windows, kiểm thử Excel, cập nhật tệp, bảo vệ không mở chồng trang.
- Kiểm thử tự động mới kiểm tra **15 trang con**, đúng 4 nhóm, mỗi trang xuất hiện đúng 1 lần, tổng quan vẫn độc lập.
- Đóng gói EXE tiếng Việt, ZIP chạy độc lập và manifest SHA-256.

## Giới hạn đang phát triển
- Việc **gộp trang** ở v0.1.2 là **gộp menu điều hướng và nhóm truy cập chức năng**; chưa gộp dữ liệu hay sửa đồng loạt các biểu mẫu nghiệp vụ thành một biểu mẫu lớn.
- Chưa triển khai ghi hai chiều vào `KHO.xlsx` và khôi phục công thức tự động. AI không tự sửa Excel mà thiếu phê duyệt.
- Cập nhật silent trong `Program Files` vẫn phụ thuộc quyền UAC.
- CI xác minh build và cấu trúc giao diện, chưa thay thế kiểm thử thao tác thực tế bằng chuột trên Windows người dùng.
- Bộ cài chưa ký số.

Phản hồi: https://github.com/infiniti-money/Kho-VT/issues
