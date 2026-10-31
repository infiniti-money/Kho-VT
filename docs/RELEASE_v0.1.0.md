# HISA Kho-VT v0.1.0 — Bản phát hành Windows chính thức đầu tiên

Bản này hợp nhất tất cả chức năng **đã được triển khai và qua kiểm thử biên dịch Windows** đến ngày 31/10/2026. Gói cài đặt và ZIP chạy độc lập được biên dịch từ **cùng commit**. Đây là bản phát hành chính thức của phạm vi chức năng hiện có, **không có nghĩa là tất cả hạng mục trên lộ trình đã hoàn tất**.

## Nội dung đã có
- Quản lý danh mục vật tư, tồn kho, giao dịch nhập/xuất/điều chuyển, phiếu chờ xác nhận, yêu cầu vật tư và đề xuất nhập kho dựa trên mức tồn tối thiểu.
- Tra cứu, báo cáo, xuất/nhập dữ liệu qua Excel theo chức năng hiện có, in phiếu/mẫu in, mã vạch và sao lưu JSON.
- Chọn và **đọc chỉ hai sheet** `Danh mục`, `Nhập Xuất` của file `KHO.xlsx` để kiểm tra cấu trúc, cảnh báo công thức thiếu và đối chiếu với file Excel khác theo SKU/số lượng.
- Nhận diện file Excel đang mở hoặc bị khóa để bảo vệ khi chuẩn bị ghi; chế độ đối chiếu đang **chỉ đọc**.
- Trang AI Gemini trò chuyện, thống kê nhập/xuất 7 và 30 ngày, cảnh báo dưới tối thiểu/hết hàng, yêu cầu phê duyệt đề xuất nhập kho. Mic/âm thanh hoạt động khi Windows có gói giọng tương ứng. Khóa Gemini được mã hóa theo tài khoản Windows, giữ khi nâng cấp.
- Giao diện mới cho trợ lý AI và cập nhật bố cục/màu sắc một số màn hình.
- Nút kiểm tra/tải phiên bản từ GitHub Releases với đối chiếu SHA-256; nếu thư mục cài cho phép ghi có thể cài và mở lại, nếu cài tại Program Files có thể cần quyền quản trị và quy trình cài thủ công.
- Bộ cài `HISA-KhoVT-Setup.exe` tiếng Việt bước đầu, thư mục mặc định `C:\Program Files\HISA\QuanLyKho`, biểu tượng Desktop và Start Menu.

## Tải và cài
- **HISA-KhoVT-Setup.exe**: bộ cài Windows x64, cần quyền quản trị để cài trong Program Files.
- **HISA-KhoVT-Windows-x64.zip**: bản ứng dụng tự chứa, giải nén **toàn bộ** và chạy `HISA Quan Ly Kho.exe`, không dùng riêng một file exe.
- **SHA256SUMS.txt**: đối chiếu checksum của hai tài sản tải về.

## Quan trọng: chức năng chưa hoàn thành
1. **Chưa có ghi hai chiều vào file `KHO.xlsx`**. Ứng dụng không được tự ý thay đổi file gốc; việc khôi phục công thức Excel sau khi bị xóa chưa được nghiệm thu.
2. AI **không tự sửa Excel**, không tự duyệt phiếu. Thống kê AI hiện lấy từ dữ liệu HISA lưu cục bộ, có thể chưa phản ánh thay đổi Excel chưa đồng bộ; tính năng giọng nói phụ thuộc Windows.
3. **Cập nhật silent trong Program Files chưa hoàn thiện**: không vượt UAC, có thể phải tải về/cài thủ công; cài silent bằng Inno Setup không tự mở lại ứng dụng.
4. Giao diện mới được tập trung ở trang AI, **chưa thiết kế lại toàn bộ các cửa sổ cũ**.
5. Chưa kiểm thử cài mới, cài nâng cấp, máy in, micro và Gemini API thật trên máy Windows của người dùng. CI mới xác minh build/publish và các smoke test về Excel cùng updater.

## Lưu ý về dữ liệu và bảo mật
- Sao lưu dữ liệu kho thành JSON trước khi thử phiên bản đầu tiên và **đóng Excel** khi cập nhật phần mềm.
- File `KHO.xlsx` gốc phải được lưu ngoài thư mục cài đặt. Không dùng bản thật để thử chức năng ghi Excel chưa được nghiệm thu.
- Kho mã nguồn GitHub hiện đang **public**. Không lưu khóa Gemini, thông tin nội bộ nhạy cảm hoặc file kho mới lên GitHub.
- EXE chưa ký số nên Windows SmartScreen có thể cảnh báo.

## Kiểm thử tự động
Workflow Windows kiểm tra build WPF, kiểm thử an toàn khóa file Excel, kiểm tra cấu trúc workbook giả lập, mô phỏng updater chặn đường dẫn ZIP không hợp lệ, đóng gói app và bộ cài. **Đây không thay thế kiểm thử thực tế trên máy người dùng.**

Báo cáo và lỗi: https://github.com/infiniti-money/Kho-VT/issues
