# HISA Kho-VT v0.2.5

## Cột ghi vào sheet Nhập Xuất của KHO.xlsx
A: Ngày giao dịch. B: Mã phiếu dùng để in (PN/PX). C: Nhập / Xuất. D: Mã vật tư. E: Tên vật tư. F: Đơn vị tính. G: Số lượng. K: Người giao nhận. L: Ghi chú và dấu mã phiếu chống ghi trùng.

H, I, J là các cột công thức bắt buộc, không nhập dữ liệu lên những ô này.

E/F có thể là công thức tra cứu của file mẫu hoặc ô nhập liệu. Phần mềm giữ nguyên công thức E/F nếu hiện có, chỉ điền tên và đơn vị tính từ danh mục hoặc Bravo nếu E/F là ô nhập liệu trống. Không ghi đè công thức cũ.

## Khôi phục
Khi Lưu phiếu, nếu thiếu công thức H/I/J hoặc công thức được bảo vệ trong Danh mục, chương trình tìm backup KHO.xlsx.HISA-*.bak gần nhất, xác minh cấu trúc, mã vật tư và công thức, tạo bản sao dự phòng trước khi khôi phục. Nếu không có backup tương thích, Excel đang mở hoặc dấu vân tay file không khớp lần đồng bộ, ngừng ghi để bảo vệ dữ liệu.

## Mã mới trong Bravo
Phiếu Nhập có SKU chưa tồn tại: tự tra file Bravo (A mã, B tên, C đơn vị), đăng ký mục mới trong Danh mục và ghi giao dịch cùng một thay thế Excel an toàn. Tồn đầu 0, không cộng thêm số lượng tồn từ Bravo; chỉ áp dụng số lượng nhập một lần.

## Khóa định danh
Mã vật tư đã tạo không chỉnh sửa được. Mã vạch Code39 cho vật tư mới tự lấy SKU và ô mã vạch chỉ đọc. Các mã vạch cũ đang khác SKU được giữ nguyên, chỉ đọc, nhằm không làm hỏng dữ liệu quét lịch sử.

## Kiểm thử
Regression test ghi cột A/B/C/D/E/F/G/K/L, bảo vệ H/I/J, giữ E/F lookup hoặc điền E/F plain input, tự phục hồi công thức từ backup đã xác minh, ngăn phục hồi khi fingerprint cũ, và bảo toàn những sheet khác.
