# Nhà trọ An Cư (NHNhaTro)

Ứng dụng quản lý nhà trọ 2 dãy A, B, mỗi dãy 4 tầng × 8 phòng (64 phòng, mã `A0.1` … `B3.8`).

- **Sơ đồ phòng**: màu theo trạng thái (trống, chưa lập phiếu, còn nợ, đã thu đủ, đang sửa), cảnh báo đồ đạc khác mặc định.
- **Nhận phòng**: chọn phòng trống, tính tiền kỳ đầu theo ngày thu (mồng 10), cọc 1 tháng vào tài khoản cá nhân,
  tiền phòng vào tài khoản hộ kinh doanh, số điện đầu lấy theo số khi khách trước trả phòng, quét CCCD nhiều người,
  in hợp đồng Word rồi tải bản đã ký lên.
- **Thu tiền tháng**: nhập số điện cả dãy, lập phiếu, thu một phần hoặc đủ, đính chứng từ, in phiếu thu PDF.
- **Trả phòng**: chốt điện, kiểm đồ đạc, phạt hư hỏng, ảnh hiện trạng, quyết toán trừ vào cọc, in biên bản.
- **Sửa chữa**: theo phòng hoặc khu vực chung, bắt buộc ảnh và chứng từ, trừ thẳng vào tài khoản cá nhân, cảnh báo sửa lặp lại.
- **Mua sắm** và **chi phí quảng cáo**: bắt buộc ảnh hóa đơn.
- **Thu chi**: số dư từng tài khoản tính từ số dư đầu kỳ, sổ giao dịch, chuyển nội bộ, báo cáo tháng, xuất Excel.
- **Quảng cáo**: trang công khai `/gioi-thieu` có ảnh, bản đồ, danh sách phòng trống kèm giá và đồ đạc, mã QR để in băng rôn.

Chi tiết nghiệp vụ và các giả định: [docs/PHAN-TICH-NGHIEP-VU.md](docs/PHAN-TICH-NGHIEP-VU.md).

## Công nghệ

| Thành phần | Dùng |
|---|---|
| Web | Blazor Web App .NET 10 (Interactive Server), Bootstrap 5, Font Awesome 6 |
| Dữ liệu | EF Core 10 + SQL Server, ASP.NET Core Identity (vai trò Admin, Staff) |
| PDF (phiếu thu, biên bản trả phòng) | QuestPDF (giấy phép Community) |
| Excel (sổ tạm trú, sổ thu chi) | ClosedXML |
| Word (hợp đồng từ mẫu) | DocumentFormat.OpenXml |
| Mã QR | QRCoder |
| Quét CCCD | API FPT.AI |

## Chạy trên máy phát triển

Yêu cầu: .NET SDK 10, SQL Server LocalDB (đi kèm Visual Studio 2026).

```bash
dotnet tool restore
dotnet run --project NHNhaTro
```

Khi khởi động, ứng dụng tự cập nhật database (migration) và tạo dữ liệu ban đầu nếu còn trống:
2 tài khoản tiền, cấu hình giá điện 3.500đ/số, nước 80.000đ/người, 6 loại đồ đạc, 2 dãy và 64 phòng.

Hoặc mở `NHNhaTro.slnx` bằng Visual Studio 2026 và bấm F5. Visual Studio 2022 không build được vì không hỗ trợ .NET 10.

Tài khoản đăng ký đầu tiên là **Quản trị**. Sau đó trang đăng ký đóng; quản trị tạo thêm tài khoản ở **Cấu hình > Người dùng**.
Nhân viên làm được mọi việc hằng ngày; chỉ quản trị sửa cấu hình và xóa giao dịch, phiếu.

### Lỗi "Your Windows doesn't fully support CET"

Trên Windows 10 chưa cập nhật, mọi file `.exe` do .NET 10 tạo ra (kể cả trình biên dịch `csc.exe`) bị tắt ngay khi chạy.
Project đã có sẵn cách né, không cần làm gì thêm:

- `Directory.Build.props`, `Directory.Build.targets` và `build/csc.cmd` cho MSBuild gọi trình biên dịch qua `dotnet.exe`.
- `<CETCompat>false</CETCompat>` trong `NHNhaTro.csproj` để `NHNhaTro.exe`, `dotnet run` và F5 chạy được.

Cài đủ bản cập nhật Windows thì có thể xóa các file này.

## Cấu hình

Phần lớn cấu hình nằm trong database và sửa trên trang **Cấu hình**: thông tin nhà trọ, giá điện nước, ngày thu,
giá và đồ đạc mặc định từng dãy, số dư đầu kỳ hai tài khoản, API key quét CCCD.
`NHNhaTro/appsettings.json` chỉ còn:

| Khóa | Ý nghĩa |
|---|---|
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối SQL Server. **Không ghi mật khẩu thật vào file này**; dùng biến môi trường `ConnectionStrings__DefaultConnection` hoặc `dotnet user-secrets` |
| `NhaTro:AllowRegistration` | Cho phép tự đăng ký tài khoản (mặc định `false`) |
| `Storage:UploadsPath` | Thư mục lưu ảnh và chứng từ (mặc định `NHNhaTro/Uploads`). Ảnh quảng cáo nằm trong `Uploads/Public`, ai cũng xem được; mọi file khác cần đăng nhập |

**Chặn lộ mật khẩu**: thư mục `.githooks` có hook chặn commit và push nếu nội dung có mật khẩu hoặc chuỗi kết nối kèm mật khẩu.
Sau khi clone, bật hook một lần:

```bash
git config core.hooksPath .githooks
```

**Mẫu hợp đồng**: `NHNhaTro/Templates/HOPDONG.docx`. Có thể chèn các trường MERGEFIELD:
`ngay`, `thang`, `nam`, `sophong`, `benthue`, `giathue`, `giathue_text`, `tiencoc`, `tiencoc_text`, `giadien`, `gianuoc`,
`ngaybatdau`, `sodiendau`, `songuoi`, `ngaythu`, `dodac`, `chunha`, `diachi`, `dienthoai`.
Mẫu hiện tại ghi cứng giá thuê, tiền cọc, giá điện nước; ứng dụng tự thay các đoạn đó bằng số của hợp đồng.

**Font PDF**: PDF dùng Times New Roman của Windows. Máy chủ không có font này thì đặt file `.ttf` vào thư mục `NHNhaTro/Fonts`.

## Triển khai lên IIS

Xem [docs/TRIEN-KHAI-IIS.md](docs/TRIEN-KHAI-IIS.md). Từ máy phát triển, triển khai lại bằng:

```bash
powershell -ExecutionPolicy Bypass -File deploy/Deploy-Server.ps1
```
