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

## Bổ sung: danh mục hàng hóa, nút nhập/xuất nhanh và một lần nhấn cập nhật

- Tab **Phân loại hàng hóa** mới trong Quản lý danh mục: thêm/bớt tên phân loại; không cho xóa loại đang được gán cho vật tư. Giữ dữ liệu `Categories` cũ, sử dụng cùng bộ lọc/danh sách hàng.
- Đổi tên **Người nhận hàng** thành **Người nhận / gửi** trong quản lý danh mục và khi lập phiếu nhập/xuất; giữ nguyên khóa dữ liệu `Recipients` và `Partner`, không đổi dữ liệu quá khứ.
- Tại trang đầu, có **Nhập nhanh** và **Xuất nhanh** riêng, tự mở phiếu ở đúng loại nghiệp vụ. Vẫn giữ nút Nhập / xuất kho chung.
- Nút **Cập nhật 1 lần nhấn** tự kiểm tra GitHub Release, tải đúng ZIP chính thức, kiểm tra SHA-256 và thực hiện cài đặt ngoài tiến trình, sau đó tự mở lại HISA. Không hỏi xác nhận thêm trong phần mềm. Nếu cài ở `Program Files`, Windows có thể hỏi quyền quản trị UAC. Không tự bỏ qua UAC. Tính năng không chạy khi HISA chưa có `HISA.Updater.exe`; khi ấy hiện ZIP được xác minh để cài thủ công.
- Khi lập **phiếu Nhập** và quét/gõ SKU chưa có, ứng dụng đọc **file danh mục đối chiếu** đã được cấu hình (không dùng file kho chính), tìm mã chính xác, lấy tên và đơn vị. Mã mới chỉ là dòng chờ, chưa được ghi vào dữ liệu thật khi người dùng hủy phiếu.
- Khi **lưu phiếu**, sau khi xác thực hash đồng bộ, HISA ghi A/B/C/D của **dòng mới trong sheet Danh mục** (mã, tên, đơn vị, tồn đầu 0) và dòng nhập mới ở sheet Nhập Xuất **trong cùng bản thay thế Excel nguyên tử có sao lưu**. Mọi ô có công thức, các sheet ngoài phạm vi và file tham chiếu được bảo toàn; cập nhật HISA sau khi Excel ghi thành công. Không được phép xuất mã mới hoặc tự thêm nếu mã không tìm thấy trong file đối chiếu.
- Nếu Excel đã có SKU nhưng HISA chưa đồng bộ hoặc HISA có tồn cục bộ với SKU Excel chưa có, thao tác từ chối và yêu cầu đối chiếu; không tính tồn hai lần.
- Không dùng số lượng tồn trong file đối chiếu để tăng tồn; chỉ số lượng của phiếu Nhập được áp dụng một lần.
- Bảo lưu giới hạn 900 dòng đã chuẩn bị sẵn trong sheet Danh mục để không sửa công thức/danh sách bên ngoài. Khi hết dòng an toàn, thông báo rõ thay vì thêm tùy tiện.

### Điều kiện kiểm thử đặc biệt
- Smoke tests phải kiểm tra tạo SKU mới trong ô trống, giữ công thức F/G/I/J/K, tồn đầu bằng 0, ghi giao dịch một lần, sao lưu và từ chối SKU không có nguồn đối chiếu.
- Bảo vệ workbook khi đang mở bằng Excel, không ghi trên fingerprint cũ, giữ nguyên công thức và các dữ liệu người dùng khác.
- Việc đọc trực tiếp file Excel đối chiếu thực và nâng quyền UAC trên máy Windows cần thử nghiệm tại máy người dùng sau cài đặt.
