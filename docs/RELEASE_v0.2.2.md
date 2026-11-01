# HISA Kho-VT v0.2.2 — Chuyển trang không còn hỏi vô lý

## Lỗi trước đây
Dù vừa mở **Nhập / Xuất kho / Điều chuyển** và chưa điền gì, bấm mục menu khác cũng bị phủ hộp thoại:
“Trang hiện tại có thể có dữ liệu chưa lưu. Chuyển trang và bỏ các thay đổi chưa lưu?”

## Cách sửa
- Mỗi biểu mẫu ghi nhận ảnh chụp các **giá trị thực** ngay sau khi được khởi tạo (sau khi ứng dụng tự điền ngày, người thực hiện, kho, mã SKU mặc định).
- **Chưa thay đổi nội dung:** chuyển trang trực tiếp, không hỏi lại.
- **Đã thêm dòng phiếu, sửa số lượng, nhập mã vật tư, đổi ngày/kho/người nhận hoặc ghi chú:** mới hỏi xác nhận trước khi bỏ các thay đổi chưa lưu.
- **Sửa rồi hoàn nguyên lại đúng trạng thái ban đầu:** chuyển trang ngay, không hỏi.
- Cả trang Nhập/Xuất, chỉnh sửa danh mục vật tư, Quản lý danh mục, Đơn vị tính, Mẫu in, Đề xuất nhập và phiếu yêu cầu đều dùng nhận biết thay đổi thật.
- Các ô đang sửa số lượng trong bảng được commit và kiểm tra trước khi quyết định bỏ trang.
- Danh sách/biểu đồ/An và những trang chỉ xem vẫn mở ngay như trước.
- Thay thông báo mơ hồ bằng “Bạn đã thay đổi dữ liệu nhưng chưa lưu. Bỏ các thay đổi này để chuyển trang?”

## Giới hạn và an toàn
- Giữ nguyên dữ liệu, Excel hai chiều, bảo vệ công thức, phân loại hàng hóa, nút nhập/xuất nhanh và cập nhật một nhấn của v0.2.1.
- Chưa cập nhật gì vào `KHO.xlsx` chỉ vì chuyển trang. Dữ liệu không lưu trong biểu mẫu được bỏ **chỉ sau khi nhấn Chuyển trang**, và chỉ khi thực sự có thay đổi.
- Chạy CI Windows build + các bộ kiểm thử Excel, updater, GUI navigation regression, PCM và bộ cài EXE/ZIP.
