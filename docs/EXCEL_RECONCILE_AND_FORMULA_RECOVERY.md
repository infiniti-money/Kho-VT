# HISA — Đối chiếu hai chiều và phục hồi công thức (PR #25)

## Cách sử dụng

Tại **Hệ thống & dữ liệu → File kho chính**, chọn đúng bản sao `KHO.xlsx`.

**Nhận thay đổi đã lưu từ Excel:**
1. Sửa `Danh mục` hoặc `Nhập Xuất` bằng Microsoft Excel, sau đó tính lại công thức và **lưu**. Thao tác trong file Excel đang mở nhưng chưa lưu không được HISA nhìn thấy.
2. HISA sẽ thông báo khi cửa sổ được kích hoạt lại nếu timestamp file khác lần đồng bộ. Chọn **Đồng bộ từ Excel (xem trước)**.
3. Kiểm tra số mã, chênh lệch số dư và lịch sử; bấm **Đồng bộ** mới cập nhật bộ nhớ HISA. HISA tạo `%LOCALAPPDATA%\HISA\QuanLyKho\SyncBackups\BeforeExcelSync-*.json` trước khi thay đổi cache.
4. Tổng tồn Excel được lấy từ **công thức F** của `Danh mục` (không lấy tổng giao dịch để cộng thêm). Các kho con HISA đang có được giữ; `Kho tổng` là phần bù để tổng HISA bằng tổng Excel. Nếu có âm số dư kho con do số tổng Excel nhỏ hơn lượng đã phân bổ, phải đối chiếu thủ công.
5. Dòng lịch sử từ Excel lưu dưới dạng `Legacy=true, AffectsStock=false`, đối chiếu bằng số dòng nguồn. Lặp lại đồng bộ không phát sinh thêm giao dịch/tồn. Phiếu HISA có dấu `[HISA:...] `trong Excel không nhập thêm lần thứ hai; phát hiện nội dung khác nhau thì chặn và yêu cầu xử lý xung đột.

**Khôi phục công thức thiếu:**
1. Chọn **Khôi phục công thức từ bản sao** và trỏ tới một file `.bak` do HISA tạo trước khi ghi hoặc một `.xlsx` đã được người dùng kiểm tra, cùng cấu trúc với nguồn.
2. HISA so khớp thứ tự mã Danh mục và **từng công thức bảo vệ đang còn** giữa file chính và bản sao. Nếu có công thức khác nhau, mẫu không cùng hàng hay ô đích không còn định dạng, **dừng** chứ không tự ước lượng công thức. Các vùng phát sinh sau bản backup cũ cũng có thể khiến khôi phục bị từ chối.
3. Xem trước số ô thiếu, rồi xác nhận. HISA chỉ chép phần tử công thức `<f>` bị mất tại **Danh mục F/G/I/J/K** và **Nhập Xuất E/F/H/I/J**, tạo bản sao lưu nguyên trạng, ghi atomically và kiểm tra các OOXML part khác không đổi. Không sửa cột nhập liệu hoặc các sheet khác. Các công thức shared được giữ thông số gốc.
4. Mở file trong Microsoft Excel, buộc tính lại và lưu trước khi đồng bộ tồn. HISA không tự tính công thức Excel trong C#.

## Quy tắc an toàn

- Tuyệt đối thử **bản sao KHO.xlsx** trước. Không đưa file thật lên GitHub.
- Nếu file đã mở/khóa, đổi sau khi xem trước, thiếu mã vật tư của một giao dịch, công thức sai khác so với bản sao hay phiếu HISA bị sửa trực tiếp trong Excel, hệ thống **dừng** và hiển thị lý do.
- Cache HISA vẫn còn các nghiệp vụ nội bộ (điều chuyển, đề xuất, phiếu chờ); các mã chỉ có ở HISA không tự bị xóa.
- Đây chưa phải cơ chế hoà giải đồng thời nhiều người. Phát hiện thay đổi dựa trên thời gian lưu khi kích hoạt ứng dụng và hành động đồng bộ có duyệt, **không đồng bộ ngầm liên tục**.
- Lỗi lưu cache sau khi ghi file và thay đổi công thức Excel không thể phục hồi tự động nếu người dùng không chọn bản sao lưu phù hợp.
- Chưa nghiệm thu end-to-end trên file thật với Microsoft Excel, máy in và phiên bản Windows người dùng; chạy CI trên workbook mô phỏng không thay thế bước này.

## Hồi quy

Smoke test `tests/ExcelAppend.SmokeTests` sẽ kiểm tra:
- đồng bộ hai lượt không cộng tồn 2 lần;
- nhận dạng riêng lịch sử từ Excel;
- khôi phục công thức I7 bị xóa từ bản `.bak` cùng mẫu;
- bản sao lưu rollback có mặt và sheet ngoài phạm vi giữ nguyên hash;
- các hồi quy ghi giao dịch an toàn và mở rộng 50 dòng sẵn có.

Chỉ merge lên `main`/phát hành sau khi CI Windows xanh và kiểm thử trên bản sao file gốc với Microsoft Excel.
