# HISA Kho-VT v0.1.7 — Hợp nhất các nâng cấp kho vật tư

Bản này tích hợp toàn bộ chức năng đã có trong v0.1.6 và những thay đổi của PR #25 (gồm nội dung PR #24). Cài đặt không xóa dữ liệu HISA hoặc thay thế `KHO.xlsx` của người dùng.

## Cải tiến chính

- **Đồng bộ Excel hai chiều có xác nhận**: đọc thay đổi đã **lưu và tính lại trong Excel** ở các sheet `Danh mục` và `Nhập Xuất`, xem trước số mã và lịch sử trước khi áp dụng. Cột **Tồn hiện tại (F)** của `Danh mục` là số dư chuẩn; **không cộng giao dịch thêm lần thứ hai**.
- **Bảo toàn nhật ký và chống trùng phiếu**: các dòng lịch sử Excel được giữ dưới dạng tham chiếu, nhận diện phiếu HISA qua dấu `[HISA:...]`, kiểm tra các phiếu có vị trí dòng nguồn để không tự nhận phiếu đã bị xóa hoặc sửa.
- **Chống ghi trên tồn kho lỗi thời**: lần đầu nâng cấp cần đồng bộ Excel trước khi ghi phiếu; mỗi lần ghi thành công lưu fingerprint SHA-256. Nếu `KHO.xlsx` đổi ngoài ứng dụng, HISA yêu cầu đối chiếu lại trước khi lập phiếu mới.
- **Khôi phục công thức có kiểm chứng**: kiểm tra công thức thiếu, nhận bản sao `.bak/.xlsx` hợp lệ, so mã thực tế kể cả `sharedStrings`, chỉ phục hồi ô công thức bị mất ở hai sheet nghiệp vụ; tạo backup trước sửa và yêu cầu Excel tính lại.
- **Mở rộng bảng Nhập Xuất an toàn**: duy trì ít nhất **50 dòng nhập liệu trống**, giữ công thức và không kéo vùng dữ liệu/validation không liên quan của các sheet khác.
- **Lỗi lưu đồng bộ**: có điểm phục hồi JSON và phục hồi trạng thái HISA đang mở khi lưu bộ nhớ sau đối chiếu thất bại.
- **Phiếu in theo mẫu Excel**: giữ tính năng xem trước/in bằng sheet `Phiếu in` của bản gốc, không viết đè mẫu chính.

## Hướng dẫn nâng cấp an toàn

1. **Sao lưu cả `KHO.xlsx` và bản JSON dữ liệu HISA** trước khi cài phiên bản này.
2. Đóng HISA và Microsoft Excel; cài `HISA-KhoVT-Setup.exe` hoặc dùng bản ZIP x64.
3. Trước khi dùng file kho đang vận hành, chọn một **bản sao `KHO.xlsx`** và mở trong Microsoft Excel để tính lại công thức, lưu và đóng file.
4. Trong HISA, vào **Hệ thống & dữ liệu → File kho chính**, chọn bản sao và bấm **Đồng bộ từ Excel (xem trước)**; xác nhận nếu số lượng tồn và phiếu chính xác.
5. Thử nhập/xuất, lưu Excel từ bên ngoài, đồng bộ lại, khôi phục công thức từ bản backup và in thử bằng Microsoft Excel. Chỉ dùng file kho thực sau khi kiểm tra đạt.

## Giới hạn và mức xác minh

- Các bài **Windows build / smoke test tự động trên workbook mô phỏng** đã đạt ở PR #25. Chưa có bằng chứng kiểm thử cuối cùng với bản sao workbook `KHO.xlsx` thật, Microsoft Excel, WebView2, máy in và toàn bộ luồng nghiệp vụ trên máy người dùng.
- Vì vậy bản phát hành này **chưa được nghiệm thu với dữ liệu sản xuất**. Không dùng trực tiếp với bản duy nhất của file `KHO.xlsx` cho tới khi kiểm thử trên bản sao thành công.
- HISA chỉ nhận những thay đổi Excel **đã lưu**, không đồng bộ im lặng khi người dùng đang nhập. Công thức Excel cần Microsoft Excel tính lại; HISA không tự tính tất cả biểu thức Excel.
- Khi ghi phiếu thành công vào Excel nhưng lưu cache HISA thất bại, không lập lại cùng phiếu; phải đối chiếu nhật ký và dấu nhận diện.
- Bộ cài chưa ký số; hệ thống có thể hiển thị cảnh báo SmartScreen.

Tài liệu kỹ thuật: `docs/EXCEL_RECONCILE_AND_FORMULA_RECOVERY.md` và `docs/EXCEL_AUTOEXPAND_AND_PRINT_SOURCE.md`.
