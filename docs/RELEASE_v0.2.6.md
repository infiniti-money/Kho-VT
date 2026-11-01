# HISA Kho-VT v0.2.6 — Phiếu yêu cầu cấp vật tư dự phòng

## Giao diện và nghiệp vụ
- Đổi mục trên menu bên trái thành **Yêu cầu vật tư dự phòng**. Bấm để mở danh sách **Phiếu yêu cầu cấp vật tư dự phòng**.
- Thay hai nút tạo yêu cầu Nhập/Cấp trong màn danh sách bằng nút **Tạo phiếu yêu cầu cấp vật tư dự phòng**. Phiếu cũ (YC-CAP, YC-NHAP) vẫn hiển thị, duyệt, in và lưu như trước.
- Trong phiếu có 2 cách nhập: chọn vật tư đã có trong danh mục hoặc bật **Nhập tay vật tư ngoài danh mục**.
- Nhập tay có các trường **Mã vật tư (không bắt buộc)**, **Tên vật tư (bắt buộc)**, **Đơn vị tính (bắt buộc)**, **Số lượng (lớn hơn 0)**. Có thể thêm nhiều dòng độc lập dù chưa có mã, sửa số lượng trước khi lưu và xóa từng dòng riêng lẻ.
- Phiếu có số dạng **YC-DP-yyyyMMdd-nnn**, ngày yêu cầu, người yêu cầu, bộ phận, người nhận, hạn cần, lý do, trạng thái và lịch sử duyệt. Có thể in theo **mẫu in riêng**, không phải phiếu Nhập/Xuất kho.
- Nút Lưu phiếu chỉ lưu dữ liệu yêu cầu trong HISA. **Không tự bổ sung vật tư vào danh mục chính, không ghi KHO.xlsx, không trừ hoặc cộng tồn, không tạo giao dịch nhập/xuất**, kể cả khi phiếu được duyệt/đánh dấu hoàn thành.
- An vẫn có thể lập đề xuất bổ sung kho theo mức tối thiểu trong luồng trợ lý riêng.

## Tương thích và kiểm thử
- Dữ liệu phiếu yêu cầu cũ, số phiếu, mẫu in và dữ liệu kho được giữ nguyên.
- Regression kiểm tra phiếu dự phòng có nhiều dòng mã để trống, tiền tố số phiếu mới, loại yêu cầu lịch sử, và không tạo sản phẩm/giao dịch.
- Windows CI kiểm tra WPF, XAML, công thức KHO, Bravo, updater, micro và bộ cài Windows.
