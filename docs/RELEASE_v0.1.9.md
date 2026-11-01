# HISA Kho-VT v0.1.9 — Sửa Gemini API và chọn micro Windows 11

Phiên bản này kế thừa v0.1.8, giữ toàn bộ dữ liệu kho, các chức năng của An, nhập/xuất, đối chiếu Excel và phiếu yêu cầu có duyệt.

## Gemini API / nhận dạng tiếng Việt
- Sửa endpoint nhận dạng chuyên dụng `gemini-3.5-transcribe`: sử dụng `POST /v1beta/interactions`, âm thanh WAV Base64 nội tuyến, mã `vi-VN`, từ vựng HISA.
- Đọc kết quả từ `output_text` hoặc `steps[].content[].text` theo định dạng Interactions của Google. Không đọc nhầm định dạng `candidates[].content[].parts` của API khác.
- Nếu model Transcribe không khả dụng cho khóa API (HTTP 400/403/404), thử **một lần** mô hình `gemini-3.5-flash-lite` để nghe hiểu giọng Việt, không thử lại khi hết hạn mức HTTP 429.
- Sửa lỗi kết nối chat do model `gemini-2.5-flash` có hạn chế cho khóa mới; chat dùng `gemini-3.5-flash-lite`. Bỏ cấu hình `temperature` không còn khuyến nghị.
- Gemini giọng nữ An sử dụng Interactions API để tổng hợp WAV qua `gemini-3.8-flash-lite-tts`, không dùng nhầm cấu trúc GenerateContent.
- Có nút **Kiểm tra API** báo riêng trạng thái truy cập Chat, nhận dạng và giọng đọc. Không gửi tệp Excel hay âm thanh trong phép kiểm tra.
- Thông báo lỗi rõ ràng khi HTTP 400, 401/403, 404, 429 hoặc lỗi máy chủ, không in khóa API, không ghi lời nói vào log.

## Chọn micro đầu vào
- Danh sách thiết bị nhận dạng từ Windows: **Mặc định Windows**, micro laptop, micro USB, tai nghe hoặc thiết bị ghi âm khác.
- Nút **Làm mới mic** hỗ trợ rút/cắm thiết bị; hiển thị **tín hiệu mic** khi thu.
- Chọn micro được lưu trong hồ sơ người dùng Windows (LocalAppData); tên/ID thiết bị được đối chiếu lại khi mở ứng dụng.
- Cả Mic một lượt và rảnh tay đều dùng cùng micro đã chọn. Khi đổi micro trong phiên, micro cũ bị dừng; cần bật lại rảnh tay và xác nhận.
- Vẫn giữ quy trình rảnh tay an toàn: người dùng đồng ý trước, phát hiện khoảng lặng 1,2 giây, tối đa 25 giây, không lưu WAV ra đĩa; dừng khi đọc bằng loa và nghỉ 750ms trước khi nghe tiếp.

## Cách kiểm tra trên Windows 11
1. Mở Settings → Privacy & security → Microphone, cho phép ứng dụng desktop sử dụng micro.
2. Mở HISA → An, chọn **Micro đầu vào**, nhấn **Làm mới mic** nếu mới cắm thiết bị.
3. Kiểm tra thanh tín hiệu khi nói. Nếu không di chuyển, đổi micro hoặc kiểm tra mức thu của thiết bị trong Windows Settings → System → Sound → Input.
4. Lưu Gemini API key trong HISA (không dán vào ChatGPT). Nhấn **Kiểm tra API** để phân biệt lỗi khóa, không có quyền mô hình hay hạn mức.
5. Nhấn **Mic**, đồng ý gửi âm thanh, nói câu "An kiểm tra tồn kho mã VT001". Sau 1,2 giây im lặng, xem bản chép tiếng Việt có dấu ở ô câu hỏi.
6. Thử rảnh tay sau khi micro và quyền API đã được xác nhận, có thể tắt bất cứ lúc nào.

## Giới hạn nghiệm thu
- CI Windows chạy build WPF, kiểm thử Excel/PCM, mô phỏng JSON Gemini Interactions, đóng gói ZIP và bộ cài EXE. Không có key API của người dùng, nên **chưa xác nhận Gemini thực tế trên máy Windows 11** hoặc loa/micro cụ thể.
- Tiếp tục sao lưu dữ liệu HISA và `KHO.xlsx` trước khi cập nhật. Không dùng bản duy nhất của file kho để thử các lệnh sửa Excel.
- Giọng Hà Nội chuẩn phụ thuộc khả năng giọng TTS/model của từng tài khoản, không thể bảo đảm trước khi nghe thử.
