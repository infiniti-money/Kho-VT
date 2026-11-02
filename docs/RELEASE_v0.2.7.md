# HISA Kho-VT v0.2.7

## Các thay đổi

- Phiếu yêu cầu cấp và nhập vật tư lưu tại sheet **Phiếu yêu cầu** trong KHO.xlsx, độc lập với bảng Nhập Xuất.
- Cập nhật thông tin và trạng thái yêu cầu có bảo vệ hash, bản sao lưu Excel trước khi ghi, không ghi lại chứng từ tồn kho.
- Có thể mở/sửa phiếu nhập xuất đang chờ xác nhận; số tồn chỉ được cập nhật khi xác nhận phiếu thực tế.
- AI An mở dưới dạng cửa sổ riêng, cho phép thao tác ở các mục kho khi đang trò chuyện/rảnh tay.
- Cài đặt micro đầu vào, ghi nhớ lựa chọn đồng ý gửi âm thanh tới Gemini (tùy chọn) trong thư mục hồ sơ người dùng Windows.

## Kiểm tra phát hành

Release workflow sẽ chạy build WPF, kiểm thử Excel (bao gồm kiểm tra các ZIP part của workbook/công thức, không thay đổi tồn), kiểm thử điều hướng, PCM voice activity, đóng gói và tính SHA-256.

**Chú ý:** Khi kiểm thử nghiệp vụ trên máy thật, hãy dùng bản sao KHO.xlsx và nghiệm thu micro, loa, màn hình Windows trước khi dùng với dữ liệu kho sản xuất. Microsoft Excel phải được lưu và đóng trước khi ghi. Nếu có lỗi, HISA từ chối ghi và giữ bản sao lưu.
