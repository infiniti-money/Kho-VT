# HISA Kho-VT v0.1.4 — Sửa lỗi giọng Việt với Gemini và chế độ rảnh tay

## Bổ sung
- **Rảnh tay Gemini tiếng Việt:** khi nhấn **Bật rảnh tay**, HISA mở hộp thoại xác nhận rõ ràng về quyền micro và việc âm thanh được gửi qua Internet tới Google Gemini. Phải chọn **Có** mới bắt đầu. Không tự bật khi khởi động phần mềm, không hỏi lại từng lượt khi phiên đã được chấp thuận.
- Thu giọng nói từ mic Windows dưới dạng **PCM 16-bit mono 16 kHz** trong bộ nhớ bằng NAudio; phát hiện tiếng nói theo RMS tại máy, giữ đệm trước tiếng nói tối đa **350 ms**; dừng sau **1,2 giây** im lặng hoặc đủ **25 giây**.
- Đoạn WAV được tạo ngay trong RAM (không tạo file âm thanh), gửi Gemini để nhận dạng tiếng Việt, sau đó tự động gửi câu hỏi đã nhận dạng sang Gemini Chat.
- **Loa và micro không hoạt động đồng thời**: trong lúc hỏi Gemini và loa Windows đọc câu trả lời, mic không ghi. Sau khi loa đọc xong chờ thêm **750 ms** mới mở mic nghe lượt tiếp.
- Nút **Tắt rảnh tay**, **Hủy micro** và **Xóa hội thoại**. Khi đổi quyền chia sẻ số liệu, thay/xóa Gemini key, chuyển trang hoặc đóng HISA thì ngắt phiên và hủy các yêu cầu đang chờ.
- Chế độ **Mic thủ công** trước đây vẫn giữ nhận dạng một lượt bằng Windows và yêu cầu người dùng kiểm tra câu hỏi rồi bấm Gửi.
- Gemini chỉ tư vấn, không tự động sửa `KHO.xlsx`, công thức hay phiếu kho.

## Giới hạn quan trọng
- Rảnh tay **liên tục sẵn sàng nghe** đến khi người dùng tắt, kể cả tiếng ồn hay tiếng người khác. Bộ phát hiện dựa trên âm lượng, **không xác minh ai đang nói**. Âm thanh đã phát hiện và nội dung được nhận dạng sẽ gửi tới Google Gemini qua Internet.
- Không gửi số liệu kho nếu người dùng chưa bật checkbox chia sẻ, nhưng **âm thanh và câu hỏi vẫn được gửi** khi bật rảnh tay.
- Windows vẫn phải cấp quyền micro, có thiết bị đầu vào hoạt động và Internet. Một số driver mic không hỗ trợ đầu vào PCM16/16kHz; HISA sẽ thông báo lỗi thay vì âm thầm chuyển phương án.
- Loa Windows cần cài giọng TTS tiếng Việt nếu muốn đọc tự nhiên. Phần mềm ưu tiên giọng nữ tiếng Việt nếu có, nếu không sẽ dùng giọng hệ thống sẵn có.
- **CI đã kiểm thử thuật toán VAD bằng PCM mô phỏng**: ngưỡng âm thanh, giới hạn đệm, im lặng 1,2s, tối đa 25s, reset/thu lại sau ngắt, header WAV; kiểm thử build WPF, Excel, updater và đóng gói installer. **Chưa thể xác nhận kết nối Gemini, micro/loa và hiệu quả chống thu âm loa bằng phần cứng thực tế** vì GitHub runner không có thiết bị và khóa Gemini người dùng.
- Vẫn chưa hoàn thiện ghi hai chiều hoặc khôi phục công thức `KHO.xlsx`, và cập nhật silent trong `Program Files` tùy thuộc quyền Windows UAC.
- Installer chưa ký số.

## Cài đặt
- Tải `HISA-KhoVT-Setup.exe`, đóng HISA cũ, sao lưu dữ liệu HISA thành JSON và cài bản mới.
- File `KHO.xlsx` nguồn và khóa API Gemini Windows nằm ngoài thư mục cài, bộ cài không chủ động ghi đè.
- Truy cập AI, kiểm tra khóa Gemini, bấm **Bật rảnh tay**, đọc hộp thoại và chọn **Có**.
- Nếu mic bị lỗi, kiểm tra Windows Settings → Privacy & security → Microphone, driver, kết nối mạng và Gemini API key.
- Có thể xem hướng dẫn kỹ thuật tại `docs/GEMINI_VI_HANDSFREE.md`.

Báo lỗi: https://github.com/infiniti-money/Kho-VT/issues
