# HISA Kho-VT v0.2.4 — Đối chiếu tồn Bravo & quyền đọc Excel cho An

## Cấu trúc file Bravo đã xác nhận

| Cột | Trường dữ liệu |
| --- | --- |
| A | Mã sản phẩm / mã vật tư |
| B | Tên vật tư |
| C | Đơn vị tính |
| L | Số lượng tồn còn lại |

- Cột L **chấp nhận tồn dương, bằng 0 hoặc âm**. Không lấy cột D/G hoặc chọn cột dựa trên tên tiêu đề tùy ý.
- Trùng mã qua nhiều dòng Bravo được cộng số lượng có dấu nếu tên và đơn vị thống nhất; dữ liệu thiếu/sai số không bị lặng lẽ tính là 0.
- So theo mã không phân biệt hoa/thường. Chênh lệch = **Bravo − KHO.xlsx**; hiển thị mã khớp, mã lệch, mã thiếu ở một trong hai nguồn và số mã tồn âm.
- **Không cộng tồn Bravo vào tồn HISA**, không sửa số dư / giao dịch / công thức và không thay đổi file Bravo khi chỉ đối chiếu.
- Người dùng chọn file **KHO.xlsx chính** và **tổng hợp Bravo .xlsx** trong phần Dữ liệu Excel; giữ nguyên đường dẫn đã lưu qua phiên bản.

## Trợ lý An

- Trong Quyền riêng tư, lựa chọn **“Cho phép An đọc tại máy KHO.xlsx và file tồn Bravo (chỉ đọc)”**. Nút **Đối chiếu tồn KHO ↔ Bravo** thực hiện đối chiếu cục bộ; An trả lời mã hàng, số tồn hai nguồn, chênh lệch và trạng thái, gồm cả âm tồn.
- Khi hỏi “đối chiếu”, “Bravo”, “mã SKU...” qua chat hoặc giọng nói, An ưu tiên đọc hai file nguồn đã lưu thay vì dựa vào bộ nhớ HISA có thể chưa đồng bộ; kết quả tính tại máy.
- **Đọc cục bộ không đồng nghĩa gửi dữ liệu lên Internet.** Chỉ khi đánh dấu riêng **Cho phép gửi số liệu tới Gemini**, ứng dụng gửi báo cáo giới hạn số dòng cho hội thoại AI. Không gửi file gốc hoặc toàn bộ bảng Bravo.
- Nếu không có quyền đọc, file chưa chọn / sai định dạng hoặc Excel đang lưu, trả lời rõ ràng; không giả định số tồn.
- Chức năng chỉnh sửa Excel của An vẫn cần xem trước và duyệt theo cơ chế có sẵn; việc cấp quyền đọc hai file không cấp quyền ghi cho Bravo.

## Kiểm thử
- Regression thực sự tạo workbook Bravo để kiểm tra A/B/C/L, tồn âm, tồn 0, mã lặp, sai số và khẳng định file nguồn không thay đổi.
- Windows build, bảo vệ KHO.xlsx, khóa workbook, quy trình cập nhật, điều hướng, PCM và bản cài EXE/ZIP được giữ nguyên.
- Chỉ nên dùng bản phát hành chính thức sau khi CI xanh; kiểm tra định dạng file xuất từ Bravo thực tế trên Windows sau khi cài.
