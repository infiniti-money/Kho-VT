# HISA — Rảnh tay Gemini tiếng Việt, PCM (phiên bản thử nghiệm)

## Quyền riêng tư
- **Không tự bật micro** khi mở HISA hoặc vào trang AI.
- Nút **Bật rảnh tay** mở hộp thoại có hai lựa chọn **Có/Không**. Phải đồng ý trước khi bắt đầu; không hỏi lại mỗi lượt khi đang trong cùng phiên.
- Sau khi đồng ý, micro luôn sẵn sàng nghe các lượt nói; âm thanh đã phát hiện có tiếng nói được gửi **qua Internet tới Google Gemini** để nhận dạng. Văn bản nhận dạng tiếp tục được gửi tới Gemini như câu hỏi.
- Khi chưa phát hiện giọng nói, HISA chỉ giữ tối đa **350ms âm thanh trong bộ nhớ**; không lưu âm thanh ra ổ đĩa, không đưa audio vào JSON, Excel hay GitHub.
- Dù người dùng không chọn chia sẻ **số liệu kho**, lời nói và nội dung câu hỏi vẫn được gửi cho Gemini khi rảnh tay bật. Dữ liệu thống kê kho chỉ được chia sẻ nếu đã bật checkbox tương ứng.
- **Nhiễu nền và lời người khác** có thể kích hoạt bộ phát hiện âm lượng. Chế độ này không có nhận dạng người nói.
- Chỉ sử dụng micro khi Windows cho phép. Không cần bộ nhận dạng Speech Recognition tiếng Việt của Windows cho **rảnh tay** vì nhận dạng trực tiếp bởi Gemini qua audio/wav.
- Chế độ Mic thủ công Windows cũ vẫn tách riêng; nhận dạng một lượt, đưa nội dung vào ô hỏi để người dùng xác nhận rồi nhấn Gửi.

## Điều khiển
1. Nhấn **Bật rảnh tay** và đọc đầy đủ hộp thoại đồng ý.
2. Giọng nói được thu dạng PCM16, 16kHz, mono; phát hiện bằng RMS trên khung 50ms, yêu cầu 2 khung vượt ngưỡng. Có đệm 350ms trước lúc nhận ra tiếng nói.
3. Dừng đoạn ghi sau **1.200ms yên lặng**, hoặc cưỡng bức sau **25.000ms**; audio PCM được gói WAV **trong RAM** và gửi Gemini để nhận dạng tiếng Việt.
4. Văn bản nhận dạng tự gửi thành câu hỏi Gemini; trợ lý chỉ trả lời và tư vấn. Không ghi Excel hay duyệt phiếu.
5. Trong lúc API đang chờ và loa Windows đọc, micro **đã ngừng**. Sau khi loa hoàn tất, chờ **750ms** rồi micro mới được mở lại.
6. **Tắt rảnh tay** hoặc **Hủy micro**: dừng luồng ghi, hủy yêu cầu đang chờ, ngắt đọc loa và ngăn mở lại.
7. **Xóa hội thoại, thay đổi quyền chia sẻ, xóa/đổi Gemini key, chuyển trang hoặc thoát ứng dụng** cũng kết thúc phiên và hủy yêu cầu đang chờ.

## Kiểm thử đã tự động hóa
- Các ca thử VAD dưới/ngưỡng âm lượng, đệm trước giọng, 1,15s chưa kết thúc, 1,20s dừng, giới hạn 25s, reset/phục hồi sau ngắt, WAV đúng header và chặn quá cỡ.
- Windows workflow biên dịch WPF, chạy các smoke tests có sẵn, publish tự chứa, tạo ZIP và biên dịch bộ cài tiếng Việt.
- **Chưa thể thử tự động mic thật, loa Windows, gói TTS tiếng Việt, Internet, quyền micro hay độ chính xác Gemini trên môi trường runner**. Trước phát hành cho người dùng nên dùng máy Windows thật để thử nhiều loại microphone và điều kiện ồn khác nhau.

## Các giới hạn kỹ thuật
- Máy cần cho phép truy cập microphone trong Windows Settings > Privacy & security > Microphone.
- NAudio cố gắng mở input PCM16 16kHz mono; một số driver không hỗ trợ trực tiếp 16kHz. Khi mở mic thất bại, HISA thông báo lỗi và giữ phiên tắt; cần thử driver hoặc bổ sung resampling ở phiên bản sau.
- Windows cần giọng TTS tiếng Việt để loa đọc tiếng Việt tự nhiên. Nếu không có, giọng mặc định khác có thể đọc sai; không có chuyện tự động cài gói ngôn ngữ.
- Chế độ Gemini Chat không có quyền tự ý sửa, lưu hoặc thay công thức file `KHO.xlsx`.
