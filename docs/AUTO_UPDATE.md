# HISA — Cập nhật qua GitHub Releases (bản thử nghiệm)

## Quy trình người dùng đã yêu cầu
1. Trong **Sao lưu & dữ liệu**, bấm **Kiểm tra & tải cập nhật**.
2. HISA truy vấn `https://api.github.com/repos/infiniti-money/Kho-VT/releases/latest`. Chỉ nhận **Release đã xuất bản** (không draft, không prerelease) có tag `vX.Y.Z` **mới hơn** phiên bản hiện tại.
3. Gói bắt buộc có đúng tên `HISA-KhoVT-Windows-x64.zip`, nguồn tải GitHub thuộc cùng repository và trường `digest: sha256:...`. Nếu thiếu digest hoặc nguồn không khớp, HISA **từ chối cài tự động**.
4. HISA tải xuống thư mục riêng `%LOCALAPPDATA%\HISA\QuanLyKho\Updates\<tag>\`, kiểm tra cả **dung lượng và SHA-256** trước khi hỏi xác nhận.
5. Nếu người dùng chọn **Có, cập nhật**, HISA lưu dữ liệu localStorage trước, chạy `HISA.Updater.exe` độc lập từ thư mục temp người dùng, **tự đóng**, trợ lý cập nhật chờ tiến trình cũ thoát, triển khai gói theo từng tệp và mở lại HISA.
6. Nếu chọn **Không, thủ công**, HISA giữ lại ZIP đã tải và mở File Explorer để tự cài sau. Nếu cài trong **Program Files** mà tài khoản không có quyền ghi, HISA chỉ cho **cài thủ công với quyền quản trị**.

## Giữ nguyên dữ liệu
- `KHO.xlsx` là nguồn riêng của người dùng. Không bao giờ đưa file này vào ZIP cập nhật hoặc chép đè file có đuôi `.xlsx`, `.xls`, `.xlsm`.
- Khóa API Gemini dùng DPAPI tại `%LOCALAPPDATA%\HISA\QuanLyKho\Secrets\gemini-key.dat`, **không nằm trong thư mục cài**.
- Bộ nhớ WebView2 và JSON của người dùng ở `%LOCALAPPDATA%\HISA\QuanLyKho\WebView2` và do trình cập nhật không đụng tới.
- `data/initial_data.json` trong **thư mục ứng dụng** là dữ liệu mẫu phát hành, có thể được cập nhật. Dữ liệu người dùng đang dùng vẫn nằm trong hồ sơ WebView2; lưu bản JSON trước khi nâng cấp lần đầu.
- Trợ lý cập nhật chặn đường dẫn ZIP không an toàn (path traversal/symlink/trùng tên), tránh ghi vào thư mục `Secrets`, `WebView2`, `UserData`; sao lưu các tệp ứng dụng bị thay đổi rồi rollback nếu một bước triển khai bị lỗi.
- Trạng thái lần cập nhật lưu cục bộ tại `%LOCALAPPDATA%\HISA\QuanLyKho\Updates\last-update.txt` (không chứa khóa API).

## Giới hạn kỹ thuật cần công bố
- **Chưa có GitHub Release chính thức** trong repository tính đến bước xây dựng này. Vì vậy chức năng kiểm tra phiên bản sẽ báo “Chưa có bản phát hành mới hơn” cho tới khi phát hành bản có tag tương ứng.
- `HISA.Updater.exe` là công cụ thay file im lặng khi thư mục cài **cho phép ghi**. Một ứng dụng Windows thường không thể ghi `C:\Program Files` trong chế độ im lặng mà không có quyền cao hơn. Phải dùng bộ cài `setup.exe` được ký hoặc trình cập nhật có dịch vụ nâng quyền được thiết kế riêng để đạt trải nghiệm cập nhật im lặng với cài đặt toàn máy. Bản này **không bỏ qua UAC**, không tự nâng quyền.
- ZIP là bản portable/ứng dụng Windows tự chứa, **chưa phải installer có giao diện cài tiếng Việt**.
- Phải thử các kịch bản: lỗi mất Internet, digest không khớp, tải dở, file khóa, quyền thư mục, hoàn nguyên khi thiếu tệp, giữ khóa Gemini và dữ liệu sau cập nhật, phát hành tag mới hơn, restart.
- Để bản mới được tự nhận diện, mỗi lần phát hành phải cập nhật `<Version>` trong `HisaWarehouse.csproj` và tạo Release có tag cùng phiên bản (ví dụ `v0.1.1`). Tệp ZIP phải lấy từ build CI đã kiểm thử đầy đủ, có đúng cấu trúc root của `publish-reconciled`.

## Kiểm thử CI
- `dotnet run --project Updater/HisaUpdater.csproj -c Release -- --self-test` kiểm tra cập nhật file giả lập, giữ file `KHO.xlsx` và chặn 6 đường dẫn ZIP nguy hiểm.
- Workflow Windows build đóng gói app và updater, tạo ZIP đúng tên Release, tải lên Artifacts làm **preview 7 ngày**, không tự phát hành GitHub Release.
- Cần kiểm thử thực tế cài đặt/update trên Windows và quyết định cách xử lý UAC trước khi đóng dấu phát hành chính thức.
