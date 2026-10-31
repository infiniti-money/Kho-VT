# Bố cục nhóm trang HISA Kho-VT

## Mục tiêu
Giảm số trang/mục chính xuất hiện đồng thời ở thanh bên và gộp các **chức năng cùng nghiệp vụ** thành 4 nhóm có thể mở rộng. Mỗi chức năng vẫn dùng biểu mẫu cũ và dữ liệu cũ; **không gộp hay xóa các bảng dữ liệu**.

| Nhóm | Trang con |
|---|---|
| **Vật tư & tồn kho** | Danh sách vật tư; Tra cứu vật tư; Cảnh báo tồn kho; Tem & mã vạch; Kho, đơn vị & bộ phận |
| **Phiếu & nhập xuất** | Nhập/Xuất kho; Phiếu chờ xác nhận; Yêu cầu vật tư; Đề xuất nhập kho; Mẫu in phiếu |
| **Báo cáo & trợ lý AI** | Nhật ký giao dịch; Báo cáo chi tiết; Trợ lý AI Gemini |
| **Hệ thống & dữ liệu** | Sao lưu & dữ liệu (bao gồm kiểm tra/đối chiếu Excel và cập nhật phần mềm); Thông tin phần mềm |

**Tổng quan** giữ ở đầu thanh bên để người dùng truy cập ngay.

## Luồng thao tác
1. Nhấn tiêu đề nhóm để mở danh sách trang con. Một lần chỉ mở rộng **một nhóm**; nhóm khác thu gọn.
2. Nhấn trang con để vào đúng chức năng, giữ nguyên thông tin phiếu đã có.
3. Khi đang mở phiếu hoặc trang thao tác, không thể mở chồng trang khác; phải **đóng trang hiện tại** trước khi chuyển.
4. Riêng **Yêu cầu vật tư → Lập phiếu yêu cầu** dùng một biểu mẫu con có kiểm soát; đóng biểu mẫu con quay về danh sách.
5. Khi AI chuyển sang đề xuất nhập, AI đóng trước, sau đó mới mở đề xuất. Hành động này không làm AI thay đổi dữ liệu kho.
6. Thanh bên chỉ làm nhiệm vụ gom trang. **Không tự sửa `KHO.xlsx`**, không thay đổi công thức hay dữ liệu WebView2.

## Kiểm thử
- WPF build Windows kiểm tra các nút và event handlers.
- `SidebarModules.SmokeTests` phân tích XAML để đảm bảo **15 trang con xuất hiện đúng một lần**, trong đúng nhóm, không bỏ sót hay nhân đôi.
- `WorkspaceNavigation.SmokeTests` xác nhận không cho mở trang chồng và điều kiện biểu mẫu con.
- Sau CI vẫn cần kiểm tra thủ công trên Windows: nhóm mở/thu, active highlight, chuyển trang, phiếu đang thao tác, màn hình độ phân giải thấp.

**Phạm vi:** Đây là gộp **điều hướng và bố cục chức năng**, không phải gộp tất cả tính năng thành một biểu mẫu sửa chung; những chức năng chuyên môn độc lập vẫn là các trang con để thao tác chính xác.
