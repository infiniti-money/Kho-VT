# HISA Kho-VT v0.2.3 — Xu hướng lên đầu Tổng quan

## Thay đổi
- Trên trang **Tổng quan**, đưa khối **Xu hướng nhập xuất và cơ cấu vật tư** lên ngay sau phần tiêu đề HISA (cùng khu vực nút Cập nhật 1 lần nhấn và An).
- Bộ biểu đồ gốc `ChartsWebView` (WebView2) được giữ nguyên hoàn toàn, bao gồm dữ liệu nhập/xuất, cơ cấu vật tư, bộ lọc và quá trình cập nhật.
- **Chức năng chính**, các nút **Nhập nhanh / Xuất nhanh**, chỉ số KPI tồn kho, bảng cần theo dõi và chất lượng dữ liệu giữ nguyên nội dung, được xếp bên dưới xu hướng.
- Không thay đổi file KHO.xlsx, công thức, quy trình ghi phiếu, đồng bộ, Gemini, mic hoặc cập nhật một nhấn.

## Kiểm thử
- Windows WPF build, smoke test XAML bảo đảm Xu hướng là phần đầu tiên dưới tiêu đề và `ChartsWebView` tồn tại đúng một lần.
- Excel write safety, formula, updater, điều hướng, PCM và bộ cài Windows giữ nguyên bài test từ v0.2.2.
- Sau cài đặt cần xem giao diện thực trên Windows để xác nhận kích cỡ biểu đồ và cuộn trang.
