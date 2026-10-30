# Phân tích workbook `KHO.xlsx`

Phân tích được thực hiện theo dữ liệu lưu trong workbook ngày 26/10/2026. File gốc được đọc để phân tích; ứng dụng không ghi ngược vào workbook.

## Cấu trúc

| Trang tính | Nội dung và nhận xét |
| --- | --- |
| `Danh mục` | Bảng vật tư chính, tiêu đề ở hàng 3. Có 460 SKU thực tế trong bảng được định dạng rộng đến hàng 900. Các trường gồm mã, tên, đơn vị, tồn đầu, tồn tối thiểu, tồn hiện tại, chênh lệch, bộ phận, số liệu phần mềm, trạng thái và đề xuất nhập. |
| `Nhập Xuất` | Nhật ký có 922 dòng ghi loại `Nhập` hoặc `Xuất`, từ 11/07/2026 đến 23/10/2026. Có thêm 4 dòng thiếu số lượng và 4 dòng thiếu loại giao dịch nhưng còn dữ liệu vật tư hoặc ghi chú. Workbook không có trường số chứng từ riêng. |
| `Báo cáo` | Bảng xem theo thời gian, dùng công thức tra từ nhật ký và có các chỉ tiêu theo ngày/tháng. |
| `Tìm kiếm` | Màn tra cứu theo mã hoặc tên, chứa một từ khóa mẫu đã nhập sẵn. |
| `Phiếu in` | Mẫu phiếu nhập kho được định dạng sẵn với ngày, người nhận, mã hàng, tên, đơn vị, số lượng, tồn sau và phần ký. |
| `Dashboard` | Có số mã hàng, tổng tồn và cảnh báo. Một số công thức nhập/xuất, tổng hợp và danh sách tồn thấp đang trả `#REF!`. |
| `Hướng dẫn` | Ghi chú cách dùng workbook và cập nhật gần nhất 23/07/2026, cũ hơn các giao dịch tháng 10. |

## Danh mục và số dư

- 460 mã SKU không trùng nhau.
- Theo số dư snapshot trong `Danh mục`: 321 mã đủ mức tối thiểu, 53 mã còn hàng nhưng dưới mức tối thiểu, 84 mã có tồn bằng 0 và 2 mã có tồn âm.
- 149 mã không có mức tồn tối thiểu dạng số. App không tính các mã này vào cảnh báo tồn thấp cho đến khi được thiết lập mức tối thiểu.
- Cột đơn vị có nhiều cách viết hoa/thường, chẳng hạn `cái` và `Cái`. App giữ đơn vị gốc và cho quản lý thêm đơn vị mới.
- Workbook không có đơn giá vốn. Tổng tồn cộng từ các dòng có đơn vị khác nhau là 444.501,7 đơn vị hỗn hợp, không phải giá trị tiền. App theo dõi số lượng và không hiển thị giá trị tồn.
- Workbook không có danh mục kho con. Vì vậy, snapshot tồn ban đầu được đưa vào `Kho tổng`; bảy kho con còn lại bắt đầu từ 0. Người dùng có thể phân bổ lại trong biểu mẫu vật tư sau khi kiểm kê.
- Workbook không có cột nhóm hàng rõ ràng. Nhóm ban đầu được app suy đoán từ tên vật tư và có thể chỉnh trong danh mục. 244 mã được xếp vào `Vật tư khác` do không khớp chắc chắn với từ khóa ban đầu.

## Nhật ký và đối chiếu

- Có 213 dòng nhập và 709 dòng xuất có loại giao dịch. Ngày giao dịch hợp lệ nằm trong khoảng 11/07/2026–23/10/2026.
- 4 dòng có loại nhưng thiếu số lượng được giữ trong nhật ký với cờ thiếu số lượng. 4 dòng khác có dữ liệu vật tư/ghi chú nhưng thiếu loại được lưu là `Chưa rõ`.
- App gắn mã tham chiếu `XL-<số dòng Excel>` cho các dòng lịch sử để tra lại vị trí trong file. Đây không phải số chứng từ gốc.
- Các dòng lịch sử chỉ dùng cho nhật ký và biểu đồ. Tồn hiện tại lấy từ snapshot trong `Danh mục`, tránh cộng giao dịch lịch sử lần thứ hai.
- Có một chênh lệch khi đối chiếu số dư đầu và giao dịch: SKU `TPHS8230` có snapshot 2.100 thanh trong danh mục, trong khi số dư đầu cộng giao dịch tính ra 2.700 thanh. Chênh lệch là 600 thanh. App giữ snapshot 2.100 và hiển thị chênh lệch để rà soát.

## Công thức và độ tin cậy

- Workbook có 2 liên kết đến workbook bên ngoài. Một số ô `Phần Mềm` dựa vào các liên kết này.
- Vùng dữ liệu có nhiều công thức kéo dài qua các hàng trống. Dashboard Excel hiện có 6 ô kết quả lưu dạng `#REF!`.
- App không dùng công thức Dashboard Excel để tính KPI. Cảnh báo tồn được tính từ snapshot, mức tồn tối thiểu và các giao dịch mới do app tạo.
- Biểu đồ xu hướng mặc định so sánh số dòng phiếu theo tháng để không cộng lẫn số lượng như cái, kg, mét và tấm. Có thể chọn một đơn vị cụ thể để xem lượng nhập/xuất cùng đơn vị.
