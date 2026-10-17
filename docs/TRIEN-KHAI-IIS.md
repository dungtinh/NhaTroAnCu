# Triển khai NHNhaTro lên IIS (Windows Server)

Tài liệu này cũng dành cho Claude Code chạy trên máy chủ: làm lần lượt từng bước, kiểm tra xong bước này mới sang bước sau.
**Không ghi mật khẩu vào file nào nằm trong git** (repo có hook chặn, xem README).

## 0. Cập nhật bản mới (đã cài đặt xong)

Máy chủ hiện tại: `WIN-2VO4M17R0NR` (192.168.1.89), site IIS `NHNhaTro` tại `C:\inetpub\wwwroot\NhaTroAnCu`,
tên miền `nhatro.ancuhaiphong.vn`, dữ liệu tải lên ở `D:\NHNhaTroData\Uploads`. Từ máy dev, một lệnh là publish và cập nhật:

```powershell
powershell -ExecutionPolicy Bypass -File deploy\Deploy-Server.ps1
```

Script dùng PowerShell Remoting với tài khoản quản trị máy chủ lưu mã hóa trong `%USERPROFILE%\.nhnhatro-server-cred.xml`
(tạo một lần bằng `Get-Credential | Export-Clixml ...`) và không ghi đè `appsettings.Production.json` trên máy chủ.
Các mục dưới đây là hướng dẫn cài từ đầu.

## 1. Điều kiện trên máy chủ

| Kiểm tra | Lệnh (PowerShell, quyền Administrator) | Cần thấy |
|---|---|---|
| ASP.NET Core Module V2 (đi kèm .NET 10 Hosting Bundle) | `Get-WebGlobalModule \| ? Name -like 'AspNetCoreModule*'` | `AspNetCoreModuleV2` |
| Runtime ASP.NET Core 10 | `dotnet --list-runtimes` | `Microsoft.AspNetCore.App 10.0.x` |
| WebSocket cho Blazor Server | `Get-WindowsFeature Web-WebSockets` | `Installed` |

Thiếu thì cài:

```powershell
# .NET 10 Hosting Bundle: tải "Hosting Bundle" bản 10.0 mới nhất tại https://dotnet.microsoft.com/download/dotnet/10.0
# rồi chạy file cài, sau đó:
Install-WindowsFeature Web-WebSockets
net stop was /y; net start w3svc
```

Cài Hosting Bundle **sau** khi đã bật IIS. Nếu IIS được bật sau thì chạy lại file cài Hosting Bundle (chọn Repair).

## 2. Publish

Trên máy chủ (đã có .NET 10 SDK) hoặc trên máy dev rồi chép thư mục sang:

```powershell
git pull
dotnet publish NHNhaTro/NHNhaTro.csproj -c Release -o C:\inetpub\NHNhaTro
```

Thư mục publish phải có `NHNhaTro.dll`, `web.config`, `wwwroot\`, `Templates\HOPDONG.docx`.
Khi publish đè lên site đang chạy, dừng app pool trước (hoặc đặt file `app_offline.htm` vào thư mục) để không bị khóa file.

## 3. Chuỗi kết nối (nguyên nhân lỗi hay gặp nhất)

Trên IIS app chạy ở môi trường **Production**: User Secrets **không** được nạp, nên app dùng chuỗi LocalDB trong
`appsettings.json` và lỗi khi truy cập database. Tạo file `C:\inetpub\NHNhaTro\appsettings.Production.json`
(file này bị git bỏ qua, chỉ nằm trên máy chủ):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=WIN-2VO4M17R0NR,9898;Database=NHNhaTro;User Id=tindd;Password=<mật khẩu>;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Nếu SQL Server cùng máy với IIS có thể dùng `Server=localhost,9898`. Giới hạn quyền đọc file này cho Administrators và app pool:

```powershell
icacls C:\inetpub\NHNhaTro\appsettings.Production.json /inheritance:r /grant:r "Administrators:F" "IIS AppPool\NHNhaTro:R"
```

Database `NHNhaTro` đã có đủ bảng (đã chạy 2 migration). Nếu cần chạy lại migration trên máy chủ:
`dotnet ef database update --project NHNhaTro` với chuỗi kết nối ở trên.

## 4. Tạo site trong IIS

```powershell
Import-Module WebAdministration
New-WebAppPool -Name NHNhaTro
Set-ItemProperty IIS:\AppPools\NHNhaTro -Name managedRuntimeVersion -Value ''          # No Managed Code
Set-ItemProperty IIS:\AppPools\NHNhaTro -Name processModel.loadUserProfile -Value $true  # khóa Data Protection, tránh mất đăng nhập
Set-ItemProperty IIS:\AppPools\NHNhaTro -Name startMode -Value AlwaysRunning
New-Website -Name NHNhaTro -PhysicalPath C:\inetpub\NHNhaTro -ApplicationPool NHNhaTro -Port 80   # đổi cổng/host theo nhu cầu

# Quyền ghi cho ảnh CCCD, scan hợp đồng và log
New-Item -ItemType Directory -Force C:\inetpub\NHNhaTro\Uploads, C:\inetpub\NHNhaTro\logs | Out-Null
icacls C:\inetpub\NHNhaTro\Uploads /grant "IIS AppPool\NHNhaTro:(OI)(CI)M"
icacls C:\inetpub\NHNhaTro\logs /grant "IIS AppPool\NHNhaTro:(OI)(CI)M"
```

Nên để thư mục Uploads ngoài thư mục publish (để publish lại không đụng tới ảnh) bằng cấu hình
`"Storage": { "UploadsPath": "D:\\NHNhaTroData\\Uploads" }` trong `appsettings.Production.json` và cấp quyền như trên.

Ảnh CCCD và scan hợp đồng của bản MVC cũ: chép thư mục `Uploads` cũ vào đường dẫn này.

## 5. Bảng tra lỗi

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| HTTP 500.19, mã 0x8007000d | Chưa cài Hosting Bundle (IIS không hiểu thẻ `aspNetCore` trong web.config) | Cài/Repair .NET 10 Hosting Bundle, `iisreset` |
| HTTP 500.31 "Failed to find a matching runtime" | Thiếu runtime ASP.NET Core 10 | Cài Hosting Bundle 10.0 |
| HTTP 500.30 "In-process start failure" | App lỗi khi khởi động | Xem Event Viewer và stdout log (mục 6) |
| HTTP 500.0 hoặc trang lỗi khi đăng nhập, xem phòng | Không kết nối được SQL (chuỗi LocalDB, sai mật khẩu, firewall cổng 9898) | Mục 3; thử `sqlcmd -S WIN-2VO4M17R0NR,9898 -U tindd -C -Q "select 1"` |
| Trang tải nhưng bấm nút không phản hồi, báo mất kết nối | Thiếu WebSocket | `Install-WindowsFeature Web-WebSockets` |
| Đăng nhập xong bị đăng xuất sau khi recycle app pool | Khóa Data Protection không lưu được | `loadUserProfile = true` (mục 4) |
| Lỗi khi tải ảnh CCCD | App pool không có quyền ghi Uploads | `icacls` ở mục 4 |
| In hợp đồng báo không tìm thấy mẫu | Thiếu `Templates\HOPDONG.docx` trong thư mục publish | Publish lại bằng bản code mới |

## 6. Xem lỗi chi tiết

1. **Event Viewer** → Windows Logs → Application, nguồn `IIS AspNetCore Module V2` và `.NET Runtime`:
   `Get-EventLog Application -Newest 20 -Source "IIS AspNetCore Module V2",".NET Runtime" | fl TimeGenerated,Message`
2. **stdout log**: trong `C:\inetpub\NHNhaTro\web.config` đổi `stdoutLogEnabled="false"` thành `"true"`, mở lại trang,
   đọc file mới nhất trong `logs\`. Xong nhớ tắt lại (log không tự xóa).
3. **Chạy tay để thấy lỗi ngay**:
   ```powershell
   cd C:\inetpub\NHNhaTro
   $env:ASPNETCORE_ENVIRONMENT = "Production"
   dotnet .\NHNhaTro.dll --urls http://localhost:5299
   ```
   App sẽ in lỗi khởi động ra màn hình; dòng `critical` về LocalDB nghĩa là chưa làm mục 3.
