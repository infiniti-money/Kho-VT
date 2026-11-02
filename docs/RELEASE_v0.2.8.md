# HISA Kho-VT v0.2.8 — Bản chính thức

## Tính năng mới

### Excel đồng bộ hai chiều
- Đọc các thay đổi từ sheet **Phiếu yêu cầu** trong KHO.xlsx, gồm phiếu yêu cầu cấp/nhập vật tư và trạng thái cập nhật trực tiếp trong Excel.
- Hiển thị thay đổi trước khi đồng bộ, giữ công thức và không cộng/trừ tồn kho lần thứ hai khi đồng bộ phiếu yêu cầu.
- Ghi phiếu yêu cầu qua cơ chế kiểm tra dấu vân tay Excel, tạo bản sao lưu và từ chối ghi nếu tệp đã thay đổi.

### Cài đặt và xem trước mẫu in
- Các khổ A4, A5, A6, Letter, 58/80 mm và tùy chỉnh đối với mẫu phiếu HISA; hướng ngang/dọc, lề bốn phía, máy in, số bản, tỷ lệ 30–200%.
- Xem trước bản in được phân trang, bố trí lại nội dung trên khổ nhỏ.
- Tùy chọn áp dụng thiết lập khổ và tỷ lệ cho PDF **Phiếu in** từ Excel (A4/A5/A6/Letter); mặc định giữ nguyên mẫu gốc. PDF dựng từ **bản sao tạm**, không sửa KHO.xlsx.

### Nhập lịch Zalo và lập phiếu chờ theo hợp đồng
- Chọn ảnh Zalo JPG/PNG/WebP, xác nhận trước khi gửi ảnh tới Gemini nhận diện hợp đồng, kiện, lịch dự kiến và vật tư. Có thể chỉnh lại kết quả.
- Tìm/lọc, chọn tất cả hoặc bỏ chọn trong phạm vi đang lọc; nhập vào danh sách Tổng hợp với chống trùng theo hợp đồng/kiện/SKU/loại công việc.
- Lưu lịch sử ngày dự kiến, cảnh báo thiếu SKU, số lượng và tồn kho.
- Lên phiếu nhập/xuất **chờ xác nhận**, cho phép rà soát/sửa trước khi lưu, ngăn lập và xác nhận trùng.
- Lịch dự kiến **không tự tạo giao dịch**, không đánh dấu nhập/xuất thực tế và **không thay đổi tồn kho**. Chỉ xác nhận giao dịch thực tế mới cập nhật sổ Excel và tồn kho.

## Bảo mật, an toàn và thử nghiệm
- CI Windows build, kiểm thử Excel an toàn/công thức, phiếu yêu cầu, lịch Zalo, điều hướng và PCM voice activity được chạy lại trước khi đóng gói.
- Nhận diện ảnh trên Google Gemini cần có khóa API và kết nối Internet; chưa thể đảm bảo nhận diện chính xác 100% khi ảnh mờ hoặc thiếu dữ liệu.
- Chưa thử nghiệm vật lý trên mọi loại máy in/driver hoặc tài khoản Gemini. Hãy kiểm tra mẫu in và phiếu trên máy Windows của bạn trước khi sử dụng chính thức.

## Cách nâng cấp
1. **Sao lưu bản gốc KHO.xlsx** và bản sao lưu HISA, đóng Excel trước khi ghi.
2. Cài đặt HISA-KhoVT-Setup.exe hoặc dùng bản ZIP Windows x64.
3. Đồng bộ Excel từ bản sao KHO.xlsx để kiểm tra công thức và trạng thái phiếu yêu cầu.
4. Thử nhận diện ảnh Zalo và chỉ xác nhận thực tế khi số hợp đồng, SKU, số lượng và tồn đã đúng.
5. Dùng **SHA256SUMS.txt** để xác minh toàn vẹn EXE và ZIP trước khi cài.
