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

### Đính chính phạm vi sau v0.2.8
- **AN chỉ hỗ trợ xuất - nhập - tồn kho vật tư**: tra cứu, đối chiếu Bravo/KHO.xlsx, báo cáo và hỗ trợ phiếu có người duyệt.
- Chức năng lịch/ảnh Zalo từng được mô tả trong ghi chú bản v0.2.8 đã được **loại khỏi mã nguồn hiện hành**. Không đọc ảnh lịch giao hàng, không tổng hợp hợp đồng/kiện và không lập phiếu chờ từ ảnh.
- Phiếu nhập/xuất chờ xác nhận thông thường vẫn được giữ; chỉ người dùng xác nhận giao dịch thực tế mới cập nhật Excel và tồn kho.
- Nếu đang dùng bản cài đặt v0.2.8 đã phát hành trước đây, cần **bản build mới** để các thay đổi mã nguồn có hiệu lực.

## Bảo mật, an toàn và thử nghiệm
- CI Windows build, kiểm thử Excel an toàn/công thức, phiếu yêu cầu, điều hướng, chống xác nhận trùng phiếu chờ và PCM voice activity được chạy lại trước khi đóng gói.
- Chưa thử nghiệm vật lý trên mọi loại máy in/driver hoặc tài khoản Gemini. Hãy kiểm tra mẫu in và phiếu trên máy Windows của bạn trước khi sử dụng chính thức.

## Cách nâng cấp
1. **Sao lưu bản gốc KHO.xlsx** và bản sao lưu HISA, đóng Excel trước khi ghi.
2. Cài đặt HISA-KhoVT-Setup.exe hoặc dùng bản ZIP Windows x64.
3. Đồng bộ Excel từ bản sao KHO.xlsx để kiểm tra công thức và trạng thái phiếu yêu cầu.
4. Kiểm thử phiếu nhập/xuất chờ xác nhận, đối chiếu SKU, số lượng, tồn kho trước khi ghi phiếu thực tế.
5. Dùng **SHA256SUMS.txt** để xác minh toàn vẹn EXE và ZIP trước khi cài.
