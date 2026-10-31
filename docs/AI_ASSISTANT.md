# HISA — Trợ lý AI Gemini và giao diện mới

## Bản triển khai
- Có trang **Trợ lý AI Gemini** nằm trong thanh điều hướng và nút **✦ Trợ lý AI** ở dashboard.
- Giao diện mới: trang trò chuyện, ô số liệu cảnh báo, nút báo cáo, quản lý khóa Gemini, xác nhận quyền gửi thống kê, mic và phát âm phản hồi.
- Phân tích **tại máy** (không cần AI/API): mức tồn hiện tại từ bộ nhớ HISA, cảnh báo hết/âm tồn, dưới tối thiểu, nhập/xuất 7 ngày và 30 ngày theo từng SKU và đơn vị. Các đơn vị khác nhau **không được cộng chung thành số lượng tổng**.
- Trò chuyện Gemini sử dụng endpoint Google `v1beta/models/gemini-2.5-flash:generateContent`; giao tiếp HTTPS. Có lịch sử trò chuyện giới hạn số lượt trong phiên; lịch sử không lưu lên đĩa.
- Nút **Xem và duyệt đề xuất nhập kho** mở biểu mẫu đề xuất có sẵn của HISA. Người dùng kiểm tra và quyết định lập phiếu; nút không trực tiếp sửa file Excel.
- **KHÔNG tự ghi vào KHO.xlsx** hoặc tự phê duyệt phiếu. Báo cáo AI có thể đề xuất nhập, xuất, chỉnh sửa nhưng luôn cần người dùng xem/duyệt; hiện chưa có bộ thực thi chỉnh sửa Excel.
- Chỉ gửi thống kê rút gọn của tối đa 24 SKU nổi bật cho Gemini khi người dùng **tích ô đồng ý gửi số liệu**. Câu hỏi người dùng vẫn được gửi qua Internet khi nhấn nút Gửi.

## Khóa API
Khóa Gemini được mã hóa bằng DPAPI `CurrentUser` và lưu tại
`%LOCALAPPDATA%\HISA\QuanLyKho\Secrets\gemini-key.dat`.
Không lưu khóa trong GitHub, nhật ký, thư mục cài đặt, `localStorage`, JSON backup, hoặc Excel.
Không xuất khóa ra màn hình; nút `Xóa khóa` xóa tệp được lưu.
Một tài khoản Windows khác không giải mã được khóa; cài lại Windows/thay tài khoản có thể phải nhập lại.
Nâng cấp ứng dụng không được xóa thư mục hồ sơ người dùng.

## Giọng nói
- Sử dụng `System.Speech` trên Windows; nút **Mic** dùng nhận dạng một lượt rồi hiện văn bản để người dùng kiểm tra trước khi gửi.
- Chỉ bật nhận dạng tiếng Việt nếu máy có **engine nhận dạng tiếng Việt**; nếu chưa có thì báo rõ, không tự nhận dạng sai bằng tiếng Anh.
- TTS ưu tiên giọng nữ tiếng Việt, kế tiếp giọng tiếng Việt bất kỳ, kế tiếp giọng nữ hệ thống; chất giọng thực tế phụ thuộc các gói ngôn ngữ có sẵn trong Windows. Không thể ép hệ thống phát giọng nữ tiếng Việt nếu chưa cài.
- Microphone/loa cần quyền truy cập từ Windows. Không tự gửi audio tới Gemini (chỉ văn bản đã nhận dạng sau khi xác nhận).

## Giới hạn và kiểm thử cần làm
1. Kiểm tra build/publish CI Windows.
2. Kiểm thử UI thật: mở trang AI, các ô KPI, báo cáo tại máy, nhập và xóa khóa, mạng lỗi/khóa sai, trò chuyện sau khi đồng ý chia sẻ.
3. Kiểm thử với tài khoản Gemini API có thật (CI không được có key) và quota phù hợp.
4. Kiểm thử mic/giọng nữ tiếng Việt trên Windows có engine được cài; Windows không có engine phải hiển thị hướng dẫn.
5. **Số liệu tồn và giao dịch trong trang AI hiện lấy từ cache HISA, chưa đồng bộ hai chiều với KHO.xlsx**. Chỉ dùng để tham khảo cho đến khi phần đồng bộ Excel được kiểm thử.
6. Chưa hoàn thiện AI kiểm tra toàn bộ lịch sử Excel trực tiếp, đề xuất sửa/duyệt từng ô, chỉnh sửa Excel, và tự động cập nhật phần mềm. Các mục này tiếp tục trong lộ trình sau.

## Bảo mật
Không đưa dữ liệu Excel thật hoặc API key lên GitHub công khai.
Không ghi dữ liệu kho vào logs. Không gọi API khi tạo báo cáo tại máy.
