# Bộ cài Windows HISA tiếng Việt

## Mục tiêu của bản thử
- Tạo `HISA-KhoVT-Setup.exe` bằng Inno Setup 6, phiên bản lấy từ `HisaWarehouse.csproj`.
- Trình cài hiện các thông báo quan trọng bằng tiếng Việt, giao diện Inno Setup kiểu hiện đại.
- Thư mục mặc định **`C:\Program Files\HISA\QuanLyKho`**, cài cho toàn máy, yêu cầu phê duyệt UAC khi cần.
- Tạo shortcut menu Start và **Desktop** (chọn sẵn).
- Có Uninstall trong Windows Apps và cho phép cài đè/upgrade với cùng `AppId`.
- Không chép Excel người dùng, không can thiệp `%LOCALAPPDATA%\HISA\QuanLyKho` (WebView2, dữ liệu và khóa Gemini). Bộ cài chỉ sao chép **tệp ứng dụng** từ `publish-reconciled`.
- Khi cài tương tác, trang hoàn thành cho phép mở HISA bằng tài khoản Windows người dùng.

## Build
Workflow Windows trên nhánh kiểm thử:
1. Build/test WPF, Excel, updater.
2. Publish app win-x64 self-contained và `HISA.Updater.exe`.
3. Tạo ZIP release-format.
4. Biên dịch `installer/HisaKho.iss` bằng Inno Setup 6, truyền phiên bản `/DMyAppVersion=...`.
5. Lưu file `installer-output/HISA-KhoVT-Setup.exe` thành artifact preview trong GitHub Actions (7 ngày).

## Cài đặt không hiện giao diện
Bộ cài hỗ trợ cú pháp Inno Setup: 
```
HISA-KhoVT-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```
**Cảnh báo quan trọng:** Nếu cài vào `C:\Program Files`, Windows vẫn có thể hiển thị hộp thoại UAC để cấp quyền. `/VERYSILENT` không cho phép vượt qua UAC. Bản Inno Setup này **không tự mở lại HISA sau khi chạy im lặng**; sau khi công cụ nâng quyền ngoài cài xong cần khởi động HISA theo tài khoản gốc (hạng mục đang phát triển).

## Nghiệm thu trước khi phát hành chính thức
- [ ] Kiểm tra giao diện cài tiếng Việt trên Windows 10/11 và biểu tượng Desktop.
- [ ] Kiểm tra cài đặt trong Program Files, UAC, cài chồng bản mới, Uninstall.
- [ ] Kiểm tra không xóa dữ liệu `KHO.xlsx`, localStorage, backup và khóa Gemini.
- [ ] Kiểm tra WebView2 runtime, giọng nói Windows, AI và dữ liệu sau cập nhật.
- [ ] Thử silent và phục hồi sau lỗi; hoàn thiện cơ chế đóng/cài/mở lại với quyền quản trị.
- [ ] Ký số EXE nếu dùng phát hành thực tế. Bản preview chưa được ký số.
- [ ] Rà soát toàn bộ bản địa hóa tiếng Việt. File `installer/Vietnamese.isl` hiện dịch các chuỗi chính và dùng `Default.isl` cho những chuỗi còn thiếu.

Không coi file CI artifact là GitHub Release chính thức; hiện không có nút xuất bản nào được tự động kích hoạt.
