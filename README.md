# Nhà trọ An Cư (NHNhaTro)

Ứng dụng quản lý nhà trọ 2 dãy A, B, mỗi dãy 4 tầng × 8 phòng (64 phòng, mã `A0.1` … `B3.8`).

- **Sơ đồ phòng**: màu theo trạng thái (trống, chưa lập phiếu, còn nợ, đã thu đủ, đang sửa), cảnh báo đồ đạc khác mặc định.
- **Nhận phòng**: chọn phòng trống, thu đủ 1 tháng tiền phòng (trả trước) vào tài khoản hộ kinh doanh và tiền cọc
  (mặc định 1.000.000đ) vào tài khoản cá nhân, không cần số điện, quét CCCD nhiều người,
  in hợp đồng Word rồi tải bản đã ký lên.
- **Thu tiền tháng** (ngày chốt mồng 10): danh sách phòng có khách, chưa thu xếp trước; chỉ nhập số điện đã dùng (kWh),
  tiền phòng, điện, nước tự tính; kỳ chốt đầu tính tiền phòng từ ngày vào đến ngày chốt; thu một phần hoặc đủ, đính chứng từ, in phiếu thu PDF.
- **Trả phòng**: nhập số điện đã dùng, kiểm đồ đạc, phạt hư hỏng, ảnh hiện trạng; kỳ chưa lập phiếu tính đủ tháng; quyết toán trừ vào cọc, in biên bản.
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
| Quét CCCD và chứng từ | Google Gemini (có gói miễn phí) hoặc Claude, chọn ở trang Cấu hình |

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
giá và đồ đạc mặc định từng dãy, số dư đầu kỳ hai tài khoản, API key quét bằng AI.
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
`ngaybatdau`, `songuoi`, `ngaythu`, `dodac`, `chunha`, `diachi`, `dienthoai` (`sodiendau` còn nhận nhưng để trống vì không còn quản lý số công tơ từng phòng).
Mẫu hiện tại ghi cứng giá thuê, tiền cọc, giá điện nước; ứng dụng tự thay các đoạn đó bằng số của hợp đồng.

**Font PDF**: PDF dùng Times New Roman của Windows. Máy chủ không có font này thì đặt file `.ttf` vào thư mục `NHNhaTro/Fonts`.

**Quét bằng AI**: chọn Gemini hoặc Claude ở **Cấu hình > Quét CCCD và chứng từ**, dán API key, bấm *Kiểm tra kết nối*.
Key Gemini miễn phí lấy tại aistudio.google.com/apikey, key Claude tại console.anthropic.com. Key lưu trong database, không nằm trong mã nguồn.
Khi đã bật, ảnh CCCD tự điền thông tin người ở; ảnh chứng từ (chuyển khoản, hóa đơn) tự điền số tiền, ngày và nội dung ở các ô thu tiền,
thu cọc, trả cọc, sửa chữa, mua sắm, thu chi. Luôn kiểm tra lại số đã điền.

**Chat nội bộ**: `/chat`, tin đến tức thì qua kết nối SignalR sẵn có của Blazor Server (ChatHub phát trong tiến trình, một máy chủ).
Gõ `@ai` đầu câu để hỏi trợ lý; trợ lý dùng Gemini/Claude đã cấu hình và được kèm tình hình phòng, nợ hiện tại.

**Cài như ứng dụng (PWA)**: gửi link `https://<tên miền>/app`; trang này có nút cài, hướng dẫn iOS/Android và mã QR.
Gồm `manifest.webmanifest`, `sw.js` (chỉ hiện trang offline khi mất mạng), biểu tượng trong `wwwroot/icons`.

## Dữ liệu nghiệm thu

```bash
NHNhaTro.exe --seed-demo    # nạp dữ liệu mẫu: 9 hợp đồng, phiếu tháng 9-10, trả phòng, sửa chữa, mua sắm, thu chi
NHNhaTro.exe --clear-data   # xóa toàn bộ dữ liệu nghiệp vụ, giữ cấu hình, phòng, tài khoản tiền, người dùng
```

Chạy trong thư mục site (trên máy chủ dùng chuỗi kết nối của `appsettings.Production.json`). Lệnh nạp chỉ chạy khi chưa có dữ liệu nghiệp vụ.
Sau khi nghiệm thu xong, chạy `--clear-data` để bắt đầu nhập thật; nhớ đặt lại số dư đầu kỳ ở trang Cấu hình.

## Triển khai lên IIS

Xem [docs/TRIEN-KHAI-IIS.md](docs/TRIEN-KHAI-IIS.md). Từ máy phát triển, triển khai lại bằng:

```bash
powershell -ExecutionPolicy Bypass -File deploy/Deploy-Server.ps1
```
