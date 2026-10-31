# KHO.xlsx gốc — hồ sơ kỹ thuật xác minh (31/10/2026)

Nguồn: tệp `KHO(1).xlsx` do người dùng cung cấp riêng trong phiên làm việc. **Không thêm workbook hoặc dữ liệu mặt hàng gốc vào GitHub công khai.** SHA-256 chỉ dùng để đối chiếu phiên bản: `3be843a123b0404da864de665de509dff5c9b75c926ce92f68ad127fb2eb2e2a`.

## Giới hạn bắt buộc
- Chỉ phân tích dữ liệu kinh doanh ở hai sheet **Danh mục** và **Nhập Xuất**. `workbook.xml` và `workbook.xml.rels` được đọc để xác định đúng đường dẫn sheet.
- Các sheet còn lại **Báo cáo, Tìm kiếm, Phiếu in, Dashboard, Hướng dẫn** không được chỉnh sửa.
- Workbook có **49 ZIP entries**, bao gồm biểu đồ, drawing, bảng Excel, các dữ liệu kết nối và 2 phần external-link; không dùng thao tác `SaveAs` toàn workbook để tránh làm thay đổi ngữ nghĩa/định dạng.
- Có nhiều công thức `XLOOKUP`, `SUMIFS`, công thức mảng và công thức tham chiếu các workbook ngoài. Giá trị cache trong ZIP **có thể cũ** nếu chưa được Excel tính và lưu lại.

## Sheet Danh mục
- Tiêu đề tại hàng **3**; bảng Excel `tbl_DanhMuc6` là `A3:K900`.
- Các mã đã có: **461**, từ dòng **4 đến 464**; còn vùng trống trong bảng từ **465 đến 900**.
- Cột:
  - **A** Mã hàng: nhập liệu
  - **B** Tên hàng: nhập liệu
  - **C** Đơn vị: nhập liệu
  - **D** Tồn đầu: nhập liệu; **không thay đổi khi nhập/xuất sau này**
  - **E** Tồn tối thiểu: **128 ô có công thức** tham chiếu nguồn ngoài, các ô khác có thể nhập liệu
  - **F** Tồn hiện tại: **897 ô công thức**; tuyệt đối không ghi số trực tiếp
  - **G** Chênh lệch: **897 ô công thức**
  - **H** Bộ phận: nhập liệu
  - **I** Phần Mềm: **897 ô công thức liên kết ngoài**
  - **J** Trạng thái2: **897 ô công thức**
  - **K** Đề xuất nhập: **897 ô công thức**
- Không được ghi vào ô E có công thức ngay cả khi thuộc nhóm cột nhập liệu.

## Sheet Nhập Xuất
- Tiêu đề tại hàng **4**; bảng Excel `tbl_NhapXuat7` là `A4:L965`.
- Có mã hàng trong **932** dòng lịch sử, từ dòng 5 đến 936.
- Trong đó **926** dòng có Loại = Nhập/Xuất (213 Nhập, 713 Xuất); **6** dòng đã có SKU nhưng thiếu loại; **4** dòng có loại hợp lệ nhưng thiếu số lượng.
- Vùng trống trong phạm vi công thức hiện tại: **937–965 = 29 dòng**. Không tự thêm dòng 966 nếu chưa mở rộng công thức và tất cả vùng tham chiếu có liên quan một cách được kiểm chứng.
- Cột:
  - **A** Ngày: nhập liệu
  - **B** In: số phiếu dùng cho sheet Phiếu in, hiện chỉ có số 1 và 2 ở một số dòng; cần quy định cách đánh số khi ghi phiếu mới
  - **C** Loại: nhập `Nhập` hoặc `Xuất`
  - **D** Mã hàng: nhập liệu
  - **E** Tên hàng: **961 ô công thức**
  - **F** Đơn vị: **961 ô công thức**
  - **G** Số lượng: vừa nhập liệu, vừa có **90 ô công thức** cộng nhiều số; phải kiểm tra ô trước khi ghi
  - **H** Tồn trước: **961 ô công thức**
  - **I** Tồn sau: **961 ô công thức**
  - **J** Cảnh báo: **961 ô công thức**
  - **K** Giao/nhận: nhập liệu
  - **L** Ghi chú: nhập liệu
- Không được ghi đè ô chứa công thức hay cập nhật trực tiếp số tồn trước/sau.
- Những ô ngày dạng text hoặc có thể sai định dạng cần kiểm tra riêng khi tổng hợp theo ngày/tuần/tháng.

## Bằng chứng kiểm thử & cảnh báo
- Bản gốc có 2 liên kết workbook ngoài; phải giữ toàn bộ externalLinks, customXml, charts, drawing và validation nguyên trạng.
- Phát hiện sẵn công thức `#REF!` trên sheet Dashboard. Đó là vấn đề **đã tồn tại trong file đầu vào**; vì theo yêu cầu chỉ hai sheet, tính năng đồng bộ **không tự sửa Dashboard**.
- `PrimaryWorkbookInspector` xác minh tiêu đề, số giao dịch chưa hoàn chỉnh, ô công thức có thể bị xóa và chỗ còn trống. Mục tiêu trước mắt là kiểm tra **chỉ đọc**, hoàn toàn không sửa workbook.

## Kế hoạch ghi sau khi nghiệm thu
1. Xây dựng đề xuất thay đổi **theo từng ô được phép**, ghi vào **bản sao riêng**, sao lưu file nguồn nguyên byte.
2. Khi ghi phải có khóa độc quyền suốt giao dịch, kiểm tra phiên bản/hash (nếu file Excel đang mở: chỉ đọc, **không ghi**), kiểm soát xung đột giữa lần đọc và ghi.
3. Giữ nguyên các ZIP part của 5 sheet không liên quan và bảo toàn `f` (formula) cùng kiểu dữ liệu/định dạng trong hai sheet nghiệp vụ.
4. Kiểm tra lại tệp bản sao trong Microsoft Excel, xác minh công thức thực sự tính lại và dữ liệu lịch sử không bị cộng trùng.
5. Chỉ khi các ca kiểm thử đầy đủ đạt mới kích hoạt lệnh ghi vào file kho chính.

File Excel gốc không nằm trong thư mục cài ứng dụng, nên nâng cấp HISA không được thay thế file hoặc API key Gemini lưu riêng ở hồ sơ Windows.
