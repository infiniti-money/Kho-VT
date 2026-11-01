# HISA Kho-VT v0.1.8 — Trợ lý An, nhận dạng tiếng Việt Windows 11

Phiên bản này kế thừa tất cả các chức năng của v0.1.7, đồng thời đổi trợ lý thành **An**.

## An: giọng nữ, hội thoại và đọc nội dung
- Hỗ trợ giao diện trợ lý **An**; giọng nữ tiếng Việt dịu dàng, ưu tiên giọng `vi-VN` cục bộ nếu Windows đã cài, không đọc tiếng Việt bằng giọng Anh.
- Rảnh tay: nếu đã bật và đồng ý gửi tiếng nói, An dùng Gemini TTS `gemini-3.8-flash-lite-tts` (giọng Leda) với hướng dẫn giọng nữ miền Bắc/Hà Nội, tốc độ vừa; nếu API không được phép, fallback sang giọng nữ tiếng Việt cục bộ. Chưa thể bảo đảm chất giọng Hà Nội trên mọi máy khi chưa nghe kiểm thử thực tế.
- Nút **An đọc nội dung mới nhất**; nếu Windows thiếu giọng Việt, hỏi đồng ý trước khi gửi nội dung tới Gemini để đọc. Không lưu âm thanh ra đĩa.
- Nút **Dừng đọc** và hủy khi đóng/chuyển trang.

## Sửa lỗi micro Windows 11
- Bỏ phụ thuộc `SpeechRecognitionEngine.InstalledRecognizers()` tiếng Việt trên nút Mic. Lấy micro mặc định và ghi PCM 16 kHz, 16 bit mono trong bộ nhớ.
- Giảm ngưỡng kích hoạt tiếng nhỏ, dừng khi ngừng nói khoảng 1,2 giây, đoạn tối đa 25 giây, không giữ âm thanh trên đĩa.
- Gemini STT: `gemini-3.5-transcribe`, ưu tiên `vi-VN`, từ vựng kho vật tư; fallback `gemini-2.5-flash` cho key không hỗ trợ mô hình chuyên dụng.
- Mic một lượt chỉ điền văn bản để người dùng duyệt; rảnh tay tự gửi lời đã nhận dạng sau khi người dùng bật và đồng ý.
- Micro ngừng khi An đọc và chỉ nghe tiếp sau khi loa phát xong và nghỉ 750 ms.

## Chạy nền
- Khi bật rảnh tay, có thêm tùy chọn **Cho An tiếp tục rảnh tay khi thu nhỏ HISA xuống khay hệ thống** (mặc định tắt).
- Biểu tượng khay có **Mở HISA** và **Dừng An và mở HISA**.
- Không tự mở mic khi khởi động HISA; dừng mic khi chuyển trang, tắt rảnh tay hoặc đóng ứng dụng. Tính năng nền chỉ hoạt động khi HISA còn chạy, không tự chạy khi Windows khởi động.

## Báo cáo và đề xuất nhập vật tư
- Hỏi `An kiểm tra mã VT0001 còn bao nhiêu` để xem tồn theo mã từ bộ nhớ HISA, nêu đơn vị, kho và ngưỡng tồn.
- Hỏi `An báo cáo tình trạng hàng` để kiểm tra mã hết/âm tồn, tồn thấp và nhập/xuất 30 ngày. Báo cáo cục bộ không gửi số liệu tới TTS Google nếu chưa đồng ý chia sẻ.
- Hỏi `An lập phiếu yêu cầu nhập vật tư` hoặc nhấn nút để mở đề xuất từ tồn tối thiểu. Khi tạo phiếu, trạng thái **Chờ duyệt**, người dùng tự nhấn **Duyệt yêu cầu**; không phát sinh giao dịch nhập kho hoặc thay đổi tồn.

## Sửa file Excel có duyệt
- Các lệnh được hỗ trợ trong bản đầu tiên:
  - `An đổi tên vật tư mã VT0001 thành Tên mới`
  - `Sửa tồn tối thiểu mã VT0001 thành 7`
- An kiểm tra file Excel đã đồng bộ, đối chiếu mã chính xác và hiển thị ô **Danh mục!B** (tên) hoặc **Danh mục!E** (tối thiểu), giá trị trước/sau. Chỉ ghi sau khi chủ kho bấm **Duyệt sửa Excel**.
- Ghi vào bản làm việc sau khi khóa file, kiểm tra hash, sao lưu `.bak` và thay thế nguyên tử; công thức và sheet khác không được chỉnh.
- An không có quyền sửa công thức, số tồn F, lịch sử Nhập Xuất hoặc bất kỳ ô Excel nào khác qua lệnh thoại.

## Kiểm thử và giới hạn quan trọng
- CI Windows chạy build, kiểm thử PCM/STT parsing, smoke test Excel với workbook mô phỏng, an toàn cập nhật và bộ cài Inno Setup.
- Chưa có kiểm thử thực tế bằng khóa Gemini của bạn, thiết bị micro/loa Windows 11, giọng Hà Nội hay bản sao thực của `KHO.xlsx`; tính năng STT/TTS phụ thuộc quyền API và Internet của người dùng.
- Trước khi nâng cấp, hãy sao lưu **cả JSON dữ liệu HISA và KHO.xlsx**. Khi thử An sửa file, **chỉ sử dụng bản sao KHO.xlsx**, xác minh các công thức sau khi Microsoft Excel tính lại. Không thử trực tiếp với bản duy nhất của file kho đang vận hành.
- Bộ cài chưa ký số. Bản v0.1.7 vẫn được giữ nguyên trên GitHub Releases làm bản dự phòng.
