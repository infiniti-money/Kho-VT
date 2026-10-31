# Kiểm thử trước khi phát hành Kho-VT

## Kiểm thử tự động

Workflow `.github/workflows/windows-build.yml` biên dịch ứng dụng WPF
với .NET 10 trên Windows và kiểm tra sự có mặt của các tệp cần thiết trong
thư mục publish. Workflow **không** tạo GitHub Release và không chứng minh
giao diện WebView2 đã chạy được trên máy thật.

## Kiểm thử thủ công trên Windows

- [ ] Mở ứng dụng lần đầu: danh mục/tồn kho khởi tạo chính xác từ `initial_data.json`.
- [ ] Sửa một vật tư và lưu, đóng ứng dụng, mở lại; thay đổi vẫn còn.
- [ ] Lập phiếu nhập/xuất và xác nhận; tồn chỉ thay đổi một lần.
- [ ] Lưu phiếu chờ xác nhận; tồn không thay đổi trước khi duyệt.
- [ ] Xuất sao lưu JSON; dùng bản sao lưu đó để phục hồi, khởi động lại và kiểm tra số dư.
- [ ] Kiểm thử khi bộ nhớ WebView2 đầy (quota): hiển thị lỗi, không thông báo lưu thành công.
- [ ] Kiểm thử phục hồi JSON khi không lưu được: không hiện thông báo phục hồi thành công, dữ liệu đang mở được khôi phục.
- [ ] Nhập Excel có SKU lặp / số lượng không hợp lệ; xác nhận cảnh báo và không cập nhật sai dữ liệu.
- [ ] In phiếu và tem Code 39, kiểm tra font, số lượng và máy in.
- [ ] Kiểm tra đường dẫn, ảnh vật tư, đồ thị WebView2 và dữ liệu sau khi chạy bản cập nhật.

## Nguyên tắc an toàn dữ liệu

- Sao lưu dữ liệu đang dùng trước khi cài bản mới hoặc phục hồi dữ liệu.
- Không xóa hồ sơ `%LOCALAPPDATA%\\HISA\\QuanLyKho\\WebView2` nếu chưa sao lưu.
- Giữ `data/initial_data.json` cạnh chương trình được phát hành.
- Chỉ phát hành bản chính thức sau khi CI xanh và hoàn thành kiểm thử trên Windows.
