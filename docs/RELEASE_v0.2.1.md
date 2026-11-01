# HISA Kho-VT v0.2.1 — Tinh gọn chức năng, báo cáo nhiều mã

## Giao diện và điều hướng
- Giữ nguyên bố cục, bộ KPI, biểu đồ, cảnh báo và hai nút **Cập nhật phần mềm / An rảnh tay** ở đầu trang. Loại bỏ lối tắt tới những trang được yêu cầu bỏ; các chức năng còn lại hiển thị theo lưới cũ.
- **Trò chuyện với An** đứng vị trí thứ hai ở menu bên trái, ngay sau Trang đầu. Chuyển trang vẫn dừng ghi âm và bảo vệ dữ liệu chưa lưu; không kích hoạt mic khi mới mở app.
- Bỏ các mục độc lập: Tra cứu vật tư, Cảnh báo tồn kho, Phiếu yêu cầu vật tư, Tem và mã vạch, Thông tin phần mềm. Không xóa dữ liệu phiếu cũ hay kho khi ẩn mục.
- Tra cứu trực tiếp trong **Danh sách vật tư** theo mã SKU, mã vạch, tên và bộ phận. Bộ lọc trạng thái còn hỗ trợ nhận biết tồn thấp, hết hàng và tồn âm.
- **Tem và mã vạch** đã tích hợp vào trang Danh sách vật tư: chọn nhiều dòng bằng Ctrl/Shift, in tem hoặc mở khu vực xem trước ngay dưới danh sách; giữ dịch vụ tạo Code 39 và máy in hiện tại. Không có tab Tem riêng.
- Trang cài đặt dữ liệu/sao lưu vẫn giữ các trường kỹ thuật cần thiết, nhưng bỏ khung giới thiệu phần mềm.

## Báo cáo chi tiết với danh sách nhiều SKU
- Có ô **LỌC NHIỀU MÃ VẬT TƯ**: nhập `VT001 VT002,VT003` hoặc dán mã ngăn cách bằng dấu cách, dấu phẩy, tab hoặc xuống dòng.
- Lọc *chính xác* từng mã, không nhầm `VT001` thành `VT0010`, không phân biệt chữ hoa/thường, tự loại trùng.
- Kết hợp với các lọc khoảng ngày, loại giao dịch, kho và từ khóa hiện có.
- Xuất báo cáo Excel chỉ dùng những dòng đang lọc. Nhấn Xóa lọc để quay lại toàn bộ báo cáo.

## An toàn
- Dữ liệu vật tư, giao dịch Excel, nội dung phiếu lịch sử, mẫu in và bản sao lưu **không bị xóa hoặc sửa** chỉ vì thay đổi giao diện.
- Khóa Gemini vẫn lưu tại hồ sơ Windows, mic/rảnh tay vẫn yêu cầu đồng ý. Bảo vệ công thức và đồng bộ Excel v0.2.0 giữ nguyên.
- CI xây dựng Windows, smoke tests menu/danh mục/báo cáo, PCM và Excel. Cần kiểm tra giao diện trực tiếp trên máy người dùng sau cài đặt.
