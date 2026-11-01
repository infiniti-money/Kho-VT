# HISA Kho-VT v0.2.0 — Giao diện theo chức năng chính, An rảnh tay ngay trang đầu

Bản cập nhật v0.2.0 kế thừa toàn bộ tính năng nhập/xuất, đồng bộ Excel hai chiều, bảo vệ công thức, in mẫu Excel, đề xuất có phê duyệt và sửa Gemini tiếng Việt của v0.1.9.

## Thay đổi giao diện
- Bỏ **nút Đóng trang** tại trang chat trợ lý An. Để trở lại trang chính hoặc mở mục khác, nhấn menu bên trái; khi rời An, HISA dừng micro rảnh tay an toàn.
- Xóa menu 5 nhóm thu gọn. **16 chức năng hiển thị trực tiếp** và có thể cuộn trong menu, không phải mở từng nhóm.
- Trang đầu được thiết kế lại để ưu tiên tác vụ: **12 lối tắt trực tiếp** (vật tư, nhập/xuất, tra cứu, phiếu chờ, yêu cầu, đề xuất nhập, cảnh báo, báo cáo, nhật ký, tem, mẫu in, dữ liệu Excel).
- Giữ lại 4 KPI, biểu đồ nhập/xuất, hàng cần theo dõi và báo cáo chất lượng dữ liệu.
- **Cập nhật phần mềm** và trạng thái cập nhật nằm ngay đầu trang. Khi bấm, HISA dùng lại chức năng kiểm tra GitHub Release, tải có SHA-256, hỏi trước khi cài đặt; không tự sửa hoặc xóa `KHO.xlsx`.
- **Trò chuyện với An** và **Trò chuyện rảnh tay** ngay trang đầu. Nút rảnh tay tự mở trang An và *chỉ* gọi luồng bật rảnh tay sau hộp thoại đồng ý rõ ràng; không mở mic khi khởi động HISA.
- Báo cáo chi tiết và nhật ký giao dịch mở riêng bằng menu; Thông tin phần mềm mở bằng lối vào riêng. Dữ liệu Excel và sao lưu được giữ để không mất chức năng cũ.

## Sử dụng
1. Mở HISA, chọn một trong 12 chức năng chính ngay trang đầu hoặc menu phẳng bên trái.
2. Để cập nhật, nhấn **Cập nhật phần mềm** trên trang đầu; xem thông báo phiên bản và duyệt nếu có bản mới.
3. Để nói chuyện với An, nhấn **Trò chuyện rảnh tay** trên trang đầu và chấp nhận hộp thoại xin quyền micro/Gemini. Micro chỉ mở sau khi đồng ý. Trong An vẫn có lựa chọn micro đầu vào và nút Thử mic.
4. Để về trang đầu từ An, nhấn **Trang đầu** trong thanh điều hướng trái. HISA hủy phiên micro khi chuyển trang. Nếu đã chủ động bật rảnh tay chạy nền, tính năng đó vẫn tuân theo cơ chế đồng ý và khay hệ thống hiện có.

## Kiểm thử và an toàn
- CI Windows kiểm tra WPF/XAML, menu phẳng và 12 lối tắt, bảo vệ công thức Excel, PCM/vi-VN, trình cập nhật và bộ cài Windows.
- **Chưa kiểm thử trực tiếp** thao tác rảnh tay trên thiết bị Windows 11 của người dùng, Gemini API key thực hoặc bản `KHO.xlsx` thật.
- Trước khi cập nhật hãy sao lưu `KHO.xlsx` và dữ liệu HISA; bản v0.1.9 vẫn được giữ trên GitHub làm bản dự phòng.
