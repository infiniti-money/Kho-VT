# Ghi phiếu Nhập/Xuất HISA vào file KHO.xlsx chính

## Quy tắc dữ liệu
**File KHO.xlsx người dùng chọn là file chính.** HISA chỉ ghi các dòng đã lưu/duyệt thuộc loại `Nhập` hoặc `Xuất` vào sheet **`Nhập Xuất`**, trong vùng bảng có công thức sẵn từ dòng 5 đến 965. Các cột:

| Cột | Nội dung | Quy tắc |
|---|---|---|
| A | Ngày | **Ghi giá trị ngày**, theo hệ ngày Excel 1900 |
| B | In | **Không tự ghi**: giữ nguyên tùy chọn/nhóm phiếu in trong Excel |
| C | Loại | **Ghi** `Nhập` hoặc `Xuất` |
| D | Mã hàng | **Ghi** SKU đã có trong sheet Danh mục |
| E | Tên hàng | **Giữ nguyên công thức** |
| F | Đơn vị | **Giữ nguyên công thức** |
| G | Số lượng | **Ghi** số dương hợp lệ |
| H | Tồn trước | **Giữ nguyên công thức** |
| I | Tồn sau | **Giữ nguyên công thức** (bao gồm công thức shared) |
| J | Cảnh báo | **Giữ nguyên công thức** |
| K | Giao/nhận | **Ghi** đối tác/người thực hiện |
| L | Ghi chú | **Ghi** ghi chú và token `[HISA:<số phiếu>]` chống ghi trùng |
| M:Q | Khu phụ trợ | **Không sửa** |

- Giữ nguyên toàn bộ 5 sheet khác, định dạng, chart, external links, bảng in, công thức đã có.
- Chỉ sửa XML sheet `Nhập Xuất` và thông số tính toán trong `xl/workbook.xml` để Excel **tự tính lại công thức khi mở file lần tiếp theo**. Không đổi nội dung một công thức nào và không ghi cứng các cột tính toán.
- Vì Excel chưa được mở lại tính toán, **giá trị cache công thức có thể vẫn là giá trị cũ** sau khi HISA ghi. Khi cần xem số tồn từ công thức trong Excel, mở file bằng Microsoft Excel để tính toán và lưu file.

## Lúc nào ghi
- **Lưu ngay** một phiếu Nhập/Xuất: HISA ghi thành công vào Excel trước, sau đó mới cập nhật số dư và nhật ký HISA.
- **Lưu chờ**: chưa ghi Excel, chưa cập nhật tồn. Chỉ ghi khi **Xác nhận** phiếu chờ.
- **Điều chuyển giữa kho nội bộ HISA**: bảng Excel hiện chỉ có hai loại Nhập/Xuất và không có cột kho nên không phát sinh ghi Excel (không thay đổi tổng tồn). Điều chuyển vẫn lưu ở HISA.

## Điều kiện an toàn
- File chính phải được chọn trước trong **Dữ liệu & phần mềm**. Nếu chưa chọn, HISA **không ghi phiếu vào tồn kho HISA** để tránh lệch nguồn.
- Nếu Excel đang mở, có tệp khóa `~$KHO.xlsx`, file bị khóa/chỉ đọc hoặc bị thay đổi giữa lúc chuẩn bị: **dừng ghi** và yêu cầu đóng Excel.
- Mỗi lần ghi tạo tệp backup cùng thư mục `KHO.xlsx.HISA-backup-...bak`.
- Ghi trong vùng **A5:L965** và chỉ vào dòng trống sau dòng giao dịch cuối, không chen vào lịch sử cũ. Khi không còn đủ dòng đến 965: **dừng, không tự mở rộng**, bởi vì các công thức Danh mục và Báo cáo cố định vùng 965. Cần phê duyệt nâng vùng công thức ở một công việc riêng.
- Nếu mất công thức bảo vệ (kể cả công thức shared), dừng ghi: **không tự dựng lại công thức** trong phiên này.
- Chặn SKU không thuộc Danh mục và token phiếu đã được ghi trước đó; không xóa dữ liệu lịch sử.
- Dùng ZIP/OOXML chỉnh đúng sheet, kiểm tra SHA-256 nội dung của mọi bộ phận ngoại trừ sheet giao dịch và thông số tính lại workbook. Chuẩn bị file mới cạnh file gốc; thay nguyên tử bằng File.Replace và lưu backup. Nếu ghi thất bại, tồn kho HISA chưa cập nhật.

## Giới hạn thực tế
- Chưa triển khai **đọc đồng bộ hai chiều liên tục** khi người dùng sửa trực tiếp Excel; thao tác mới chỉ ghi từ phiếu của HISA sang KHO.xlsx. Cần giai đoạn xử lý đồng bộ trạng thái/kho ngoài, đối chiếu chênh lệch và phục hồi từ backup.
- Khi Excel được tính toán lại sau một lần ghi, công thức có thể phát sinh sai lệch nếu dữ liệu lịch sử trong file gốc vốn không nhất quán; HISA không tự sửa công thức hay các dòng cũ.
- Nếu Excel đã nhận giao dịch nhưng lưu nhật ký HISA thất bại, cần phục hồi/đối chiếu bằng token số phiếu; **không tạo lại phiếu tùy tiện**.
- Khi chuyển sang file KHO.xlsx khác, HISA phải kiểm tra lại cấu trúc và đường dẫn.
- Bộ kiểm thử mô phỏng không thay thế kiểm nghiệm giao diện, Microsoft Excel và file thật **trên bản sao được người dùng cho phép**, không đẩy file chứa dữ liệu kinh doanh lên kho GitHub công khai.
