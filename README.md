# HISA · Quản lý kho vật tư

Ứng dụng Windows viết bằng C# WPF. Các biểu mẫu nghiệp vụ hiển thị ngay trong khung nội dung của cửa sổ chính, không mở cửa sổ ứng dụng con; lỗi nhập liệu được báo bằng thông báo ngay trong app. Giao diện gồm tổng quan, danh mục tồn, tìm kiếm nhanh, quản lý danh mục kho/đơn vị/bộ phận/người nhận, cảnh báo âm kho, phiếu nhập/xuất/điều chuyển có hàng chờ xác nhận, phiếu yêu cầu cấp/nhập vật tư, mẫu in chỉnh sửa được, nhật ký, báo cáo chi tiết, đối chiếu tồn thực tế từ Excel, tem mã vạch, ảnh vật tư và sao lưu. Logo được dùng trong cửa sổ và biểu tượng ứng dụng. Hai biểu đồ dùng Chart.js 4.5.1 trong WebView2; tệp Excel được đọc và tạo bằng ClosedXML.

## Chạy từ mã nguồn

1. Cài .NET 10 SDK cho Windows.
2. Mở `run_from_source.bat`, hoặc chạy `dotnet run --project HisaWarehouse.csproj` trong thư mục này.
3. Nếu máy chưa có Microsoft Edge WebView2 Runtime, cài runtime Evergreen của Microsoft rồi mở lại ứng dụng.

## Mở bản Windows có sẵn

Chạy `open_app.bat`, hoặc mở `publish-reconciled/HISA Quan Ly Kho.exe`.

## Tạo lại bản Windows

Chạy `build_windows.bat`. Bản phát hành self-contained nằm trong `publish-reconciled`. Thư mục phát hành cần giữ các tệp nội dung `Assets`, `data`, `Web` và `static` cạnh chương trình. WebView2 Runtime vẫn cần được cài trên Windows.

## Dữ liệu và lưu trữ

Lần chạy đầu, ứng dụng nạp danh mục và lịch sử từ `data/initial_data.json`. Những thay đổi sau đó được lưu bằng `localStorage` của WebView2 trên máy hiện tại. Hồ sơ WebView2 được giữ tại `%LOCALAPPDATA%\HISA\QuanLyKho\WebView2`. Dùng **Sao lưu & dữ liệu** để xuất hoặc phục hồi tệp JSON khi chuyển máy.

App chỉ tạo giao dịch mới cho các phiếu nhập, xuất, điều chuyển và điều chỉnh. Các dòng lịch sử nhập từ Excel được giữ làm nhật ký tham chiếu và không cộng lại vào số dư snapshot. Phiếu nhập kho và phiếu xuất kho có thể in từ Nhật ký giao dịch sau khi lưu.

## Nhập và xuất Excel

Trong danh mục tồn kho, dùng **Mẫu Excel** để tạo mẫu có các cột SKU, tên, nhóm, đơn vị, bộ phận và số dư theo kho; **Xuất Excel** để tải danh mục hiện tại; **Nhập Excel** để thêm hoặc cập nhật hàng hóa. Ứng dụng cũng nhận cột `Mã hàng`, `Tên hàng`, `Đơn vị`, `Tồn tối thiểu`, `Bộ phận` và `Tồn hiện tại` từ workbook kho. Các số dư mới nhập sẽ tạo dòng điều chỉnh trong nhật ký.

Ảnh vật tư được nén trước khi lưu trong hồ sơ ứng dụng và bản sao lưu. Dung lượng ảnh tối đa toàn bộ danh mục là 2 MB để giữ dữ liệu cục bộ ổn định.

## Hàng chờ phiếu và đối chiếu tồn

Khi lập phiếu nhập/xuất/điều chuyển, tích **Lưu phiếu ở trạng thái chờ xác nhận** để giữ phiếu trong danh sách chờ mà chưa làm thay đổi tồn. Khi xác nhận, app kiểm tra lại số tồn hiện tại cho phiếu xuất/điều chuyển rồi mới ghi sổ.

Menu **Yêu cầu vật tư** tạo phiếu yêu cầu cấp hoặc nhập vật tư. Các yêu cầu giữ trạng thái riêng, không cập nhật tồn cho đến khi người dùng thực hiện phiếu kho tương ứng. Menu **Mẫu in phiếu** cho phép sửa đơn vị, tiêu đề, phần đầu/cuối và chữ ký; các mẫu được lưu cục bộ.

Trong **Sao lưu & dữ liệu**, chọn một thư mục có tệp `.xlsx` (app lấy tệp được cập nhật gần nhất) hoặc chọn thẳng một workbook. Trang **Đối chiếu nguồn tồn** so sánh SKU/Mã hàng và cột Tồn thực tế/Tồn hiện tại với tổng tồn trên app. Chọn kho ghi điều chỉnh, sửa cột **Tồn đề xuất**, chọn các dòng rồi áp dụng; phần chênh lệch được ghi thành giao dịch Điều chỉnh trong nhật ký. SKU chỉ có ở nguồn cần được thêm vào danh mục trước khi cập nhật tồn.

## Mã vạch và phiếu in

Tem SKU dùng chuẩn Code 39, hiển thị xem trước và in nhiều mã theo lưới A4. Phiếu nhập kho, xuất kho và điều chuyển có số chứng từ, kho, người thực hiện, vật tư, số lượng và khu vực ký nhận. Máy quét barcode hoạt động như bàn phím: đặt con trỏ vào ô quét, quét mã rồi nhấn Enter.

## Cấu trúc mã nguồn

- `MainWindow.xaml` và `MainWindow.xaml.cs`: cửa sổ Windows, điều hướng và các màn hình nghiệp vụ.
- `ItemEditorWindow.*`, `MovementWindow.*`, `PendingMovementsWindow.*`, `MaterialRequestsWindow.*`, `MaterialRequestEditorWindow.*`, `PrintTemplateWindow.*`, `CatalogsWindow.*`, `QuickSearchWindow.*`, `NegativeStockWindow.*`, `DetailedReportsWindow.*`, `ReferenceInventoryWindow.*`: biểu mẫu vật tư, luồng phiếu, danh mục và tra cứu/báo cáo.
- `Services/InventoryStore.cs`: lưu trạng thái vào localStorage của WebView2.
- `Services/BarcodeService.cs`: tạo hình Code 39 cho tem.
- `Services/ExcelInventoryService.cs`: tạo mẫu, nhập danh mục và xuất báo cáo Excel.
- `Services/ImageDataService.cs`: nén và hiển thị ảnh vật tư.
- `Assets/hisa_logo.png`, `Assets/hisa.ico`: logo giao diện và icon Windows.
- `Web/charts.html`, `Web/charts.js`: hai biểu đồ Chart.js.
- `data/initial_data.json`: dữ liệu khởi tạo được trích từ workbook nguồn.
- `PHAN_TICH_KHO.md`: phân tích workbook và quy tắc nhập số dư ban đầu.
