# Nhà trọ An Cư (NHNhaTro)

Ứng dụng quản lý nhà trọ: sơ đồ phòng, hợp đồng cá nhân và công ty, người thuê, phiếu báo tiền điện nước,
thu tiền và in hóa đơn, thu chi, sổ đăng ký lưu trú.

Đây là bản viết lại trên **.NET 10 / Blazor Web App (Interactive Server)** của ứng dụng ASP.NET MVC 5 cũ (NhaTroAnCu).
Bản mới dùng lại nguyên cấu trúc bảng của database cũ, nên chạy được trên dữ liệu hiện có sau khi nâng cấp bảng tài khoản.

## Công nghệ

| Thành phần | Dùng |
|---|---|
| Web | Blazor Web App .NET 10, Bootstrap 5, Font Awesome 6 |
| Dữ liệu | EF Core 10 + SQL Server, ASP.NET Core Identity |
| PDF (phiếu báo, hóa đơn, danh sách khách) | QuestPDF (giấy phép Community) |
| Excel (danh sách hợp đồng, sổ lưu trú) | ClosedXML |
| Word (in hợp đồng từ mẫu) | DocumentFormat.OpenXml |
| Quét CCCD | API FPT.AI |

Bản cũ dùng Aspose.Words, FlexCel, Xceed (trả phí) và iTextSharp; bản mới chỉ dùng thư viện miễn phí.

## Chạy trên máy phát triển

Yêu cầu: .NET SDK 10, SQL Server LocalDB (đi kèm Visual Studio 2026).

```bash
dotnet tool restore
dotnet ef database update --project NHNhaTro
dotnet run --project NHNhaTro
```

Hoặc mở `NHNhaTro.slnx` bằng Visual Studio 2026 và bấm F5. Visual Studio 2022 không build được vì không hỗ trợ .NET 10.

Lần đầu, vào **Đăng ký** để tạo tài khoản. Trang đăng ký chỉ mở khi database chưa có tài khoản nào,
hoặc khi đặt `NhaTro:AllowRegistration` là `true`, vì ứng dụng chứa ảnh CCCD và thông tin cá nhân.
Mọi trang nghiệp vụ và mọi file tải về đều yêu cầu đăng nhập.

### Lỗi "Your Windows doesn't fully support CET"

Trên Windows 10 chưa cập nhật, mọi file `.exe` do .NET 10 tạo ra (kể cả trình biên dịch `csc.exe`) bị tắt ngay khi chạy.
Project đã có sẵn cách né, không cần làm gì thêm:

- `Directory.Build.props`, `Directory.Build.targets` và `build/csc.cmd` cho MSBuild gọi trình biên dịch qua `dotnet.exe`.
- `<CETCompat>false</CETCompat>` trong `NHNhaTro.csproj` để `NHNhaTro.exe`, `dotnet run` và F5 chạy được.

Cài đủ bản cập nhật Windows thì có thể xóa các file này.

## Cấu hình (`NHNhaTro/appsettings.json`)

| Khóa | Ý nghĩa |
|---|---|
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối SQL Server. **Không ghi mật khẩu thật vào file này**; dùng biến môi trường `ConnectionStrings__DefaultConnection` hoặc `dotnet user-secrets` |
| `NhaTro:Name`, `Address`, `Owner`, `OwnerIdentityCard`, `Phone` | Thông tin nhà trọ in ở chân trang và trên chứng từ |
| `NhaTro:RegisterArea` | Địa bàn in trên tiêu đề sổ đăng ký lưu trú |
| `NhaTro:PaymentDueDay` | Ngày hạn đóng tiền in trên hóa đơn (mặc định 10) |
| `NhaTro:AllowRegistration` | Cho phép tự đăng ký tài khoản (mặc định `false`) |
| `Storage:UploadsPath` | Thư mục lưu ảnh CCCD và scan hợp đồng (mặc định `NHNhaTro/Uploads`) |

**Chặn lộ mật khẩu**: thư mục `.githooks` có hook chặn commit và push nếu nội dung có mật khẩu hoặc chuỗi kết nối kèm mật khẩu.
Sau khi clone, bật hook một lần:

```bash
git config core.hooksPath .githooks
```

**Quét CCCD**: thêm API key FPT.AI vào bảng `FPTReaderAPI` (giống bản cũ):

```sql
INSERT INTO FPTReaderAPI (ApiKey, Email, Status) VALUES (N'<api-key>', N'<email>', 1);
```

**Mẫu hợp đồng**: `NHNhaTro/Templates/HOPDONG.docx`, dùng các trường MERGEFIELD `ngay`, `thang`, `nam`, `sophong`, `benthue`.
Có thể thêm các trường `giathue`, `giathue_text`, `giadien`, `gianuoc`, `tiencoc`, `tiencoc_text`, `ngaybatdau`,
`ngayketthuc`, `sothang`, `ngaynhanphong`, `ghichu` vào mẫu.

**Font PDF**: PDF dùng Times New Roman của Windows. Máy chủ không có font này thì đặt file `.ttf` vào thư mục `NHNhaTro/Fonts`.

## Triển khai lên IIS

Xem [docs/TRIEN-KHAI-IIS.md](docs/TRIEN-KHAI-IIS.md): điều kiện máy chủ, publish, chuỗi kết nối, tạo site và bảng tra lỗi.

## Chuyển từ bản MVC 5 cũ sang

1. **Sao lưu database** đang chạy.
2. Chạy `Database/upgrade-from-mvc5.sql` trên database đó. Script chỉ bổ sung bảng tài khoản theo ASP.NET Core Identity
   và ghi lịch sử migration, không đổi dữ liệu nghiệp vụ. Tài khoản và mật khẩu cũ vẫn đăng nhập được.
   Script chạy lại nhiều lần không lỗi, và bản MVC cũ vẫn chạy song song được.
3. Chép thư mục `Uploads` (ảnh CCCD `TenantPhotos`, scan hợp đồng `ContractScans`) từ máy chủ cũ sang thư mục Uploads của bản mới.
   Đường dẫn lưu trong database giữ dạng `/Uploads/...` nên không cần sửa dữ liệu.
4. Đặt chuỗi kết nối qua biến môi trường rồi chạy ứng dụng.

> **Bảo mật**: file `Web.config` của bản cũ trên GitHub chứa mật khẩu SQL Server thật và vẫn còn trong lịch sử git.
> Hãy đổi mật khẩu tài khoản SQL đó.

## Khác biệt so với bản cũ

- Phiếu báo tiền tính theo từng phòng của hợp đồng. Bản cũ chỉ giữ một phiếu cho mỗi hợp đồng mỗi tháng nên hợp đồng công ty nhiều phòng bị ghi đè.
- Tiền phụ thu tháng đầu tính theo số ngày chênh lệch. Bản cũ luôn ra 0.
- Ghi chú gia hạn ghi đúng ngày kết thúc cũ. Bản cũ ghi ngày mới hai lần.
- Số dư đặt cọc lũy kế cộng dồn từ tháng cũ nhất.
- Hợp đồng quá hạn (Expired) vẫn hiện trên sơ đồ phòng màu đỏ và vẫn lập phiếu, thu tiền được cho tới khi kết thúc.
- Bỏ các phần không dùng của bản cũ: trang báo cáo trong Home, trang lịch sử trong Hợp đồng (gọi action không tồn tại),
  các view và stored procedure trong database không được code gọi tới.
- Không xóa được hợp đồng đã có giao dịch thu chi; hãy kết thúc hợp đồng thay vì xóa.
