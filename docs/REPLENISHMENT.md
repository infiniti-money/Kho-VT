# Đề xuất nhập kho (dự thảo tính năng)

Màn hình mới **Đề xuất nhập kho** nằm ngay dưới **Yêu cầu vật tư** ở menu trái. Gợi ý được tính theo kho đang chọn:

```
Số lượng đề xuất = max(0, tồn tối thiểu - tồn hiện tại - số lượng đang chờ nhập)
```

- Chỉ đưa vào danh sách vật tư còn hoạt động, có `MinStockDefined=true` và mức tối thiểu lớn hơn 0.
- Hiển thị cả hàng hết tồn và hàng âm tồn.
- Chỉ trừ các **Yêu cầu nhập vật tư** do quy trình này tạo cho đúng kho, đang ở trạng thái **Chờ xử lý**. Yêu cầu cũ không lưu thông tin kho phải được kiểm tra thủ công.
- Có thể chọn/bỏ chọn các dòng và sửa số lượng đề nghị. Người đề nghị là bắt buộc.
- Bấm **Tạo yêu cầu nhập kho** sẽ lập số phiếu `YC-NHAP-yyyyMMdd-NNN` theo hệ thống hiện có. Yêu cầu không thay đổi số tồn và không tự tạo phiếu nhập kho.
- Kho đề nghị được lưu trong `MaterialRequest.Warehouse` và hiển thị trên danh sách yêu cầu và phiếu in. JSON cũ thiếu thuộc tính `Warehouse` vẫn đọc được (giá trị mặc định rỗng).
- `MinStock` hiện là một giá trị cho mã vật tư, áp dụng theo kho được chọn; chưa có cấu hình tồn tối thiểu riêng cho từng kho.

## Kiểm thử hồi quy cần hoàn thành trên Windows

1. Chọn kho có SKU tồn = 2, tối thiểu = 10, chưa chờ nhập: số lượng đề xuất = 8.
2. Chọn kho có tồn = 0 hoặc âm: vẫn có gợi ý; tồn tối thiểu không khai báo thì bỏ qua.
3. Tạo yêu cầu 5 đơn vị cùng SKU, mở lại đề xuất cho kho đó: số lượng gợi ý giảm 5.
4. Đánh dấu yêu cầu hoàn thành: đề xuất trở lại theo số tồn thực tế nếu chưa có phiếu nhập; không thay đổi tồn bởi bản thân yêu cầu.
5. Sửa số lượng về 0, NaN hoặc trống rồi tạo phiếu: bị từ chối.
6. Chọn kho khác: chỉ lấy số tồn và yêu cầu đang chờ của kho mới.
7. Tạo phiếu và in, thoát ứng dụng rồi mở lại: thông tin kho và số lượng còn nguyên.
8. Kiểm tra nhập/xuất, yêu cầu thủ công, phục hồi JSON và ghi dữ liệu trên Windows trước khi gộp.
