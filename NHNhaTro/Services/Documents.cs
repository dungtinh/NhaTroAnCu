using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Document = QuestPDF.Fluent.Document;

namespace NHNhaTro.Services;

/// <summary>Chứng từ: phiếu thu (PDF), quyết toán trả phòng (PDF), hợp đồng (Word), sổ lưu trú và sổ thu chi (Excel), mã QR.</summary>
public class Documents(IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AccountService accounts)
{
    public const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static TextStyle Base => TextStyle.Default.FontFamily("Times New Roman", "Arial", "Lato").FontSize(12);

    // ============================ PHIẾU THU (PDF) ============================

    public async Task<(byte[] Bytes, string FileName)?> BillPdfAsync(int billId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var bill = await db.Bills.AsNoTracking().Include(b => b.Payments).ThenInclude(p => p.Account)
            .Include(b => b.Lease).ThenInclude(l => l.Room)
            .Include(b => b.Lease).ThenInclude(l => l.Tenants).ThenInclude(t => t.Tenant)
            .AsSplitQuery().FirstOrDefaultAsync(b => b.Id == billId);
        if (bill == null) return null;
        var rep = bill.Lease.Tenants.OrderByDescending(t => t.IsRepresentative).Select(t => t.Tenant.FullName).FirstOrDefault() ?? "";
        decimal paid = bill.Payments.Sum(p => p.Amount);
        var lines = new List<(string Item, string Detail, decimal Amount)>();
        if (bill.RentAmount != 0) lines.Add(("Tiền phòng", $"{bill.PeriodFrom:dd/MM/yyyy} - {bill.PeriodTo:dd/MM/yyyy}", bill.RentAmount));
        if (bill.MeterEnd != null || bill.ElectricityAmount != 0)
            lines.Add(("Tiền điện", $"{bill.MeterStart} → {bill.MeterEnd} = {(bill.MeterEnd ?? 0) - (bill.MeterStart ?? 0)} số × {Fmt.Money(s.ElectricityPrice)}đ", bill.ElectricityAmount));
        if (bill.WaterAmount != 0) lines.Add(("Tiền nước", $"{bill.PersonCount} người × {Fmt.Money(s.WaterPricePerPerson)}đ" + (bill.Kind == BillKind.MoveIn ? " (theo số ngày)" : ""), bill.WaterAmount));
        if (bill.OtherAmount != 0) lines.Add(("Khoản khác", bill.Kind == BillKind.MoveOut ? "Phạt / khác khi trả phòng" : "", bill.OtherAmount));
        if (bill.Discount != 0) lines.Add(("Giảm trừ", "", -bill.Discount));

        var doc = Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(Base.FontSize(11));
            page.Header().Column(col =>
            {
                col.Item().Text(s.Name.ToUpper(Fmt.Vi)).Bold();
                if (!string.IsNullOrWhiteSpace(s.Address)) col.Item().Text(s.Address).FontSize(9);
                col.Item().PaddingTop(6).AlignCenter().Text($"PHIẾU THU TIỀN PHÒNG {bill.Lease.Room.Code}").FontSize(15).Bold();
                col.Item().AlignCenter().Text($"{BillKind.Display(bill.Kind)} - tháng {bill.Month:00}/{bill.Year} - Số phiếu: {bill.Id:D6}").FontSize(10);
            });
            page.Content().PaddingVertical(8).Column(col =>
            {
                col.Spacing(4);
                col.Item().Text($"Người thuê: {rep}");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(5); cd.RelativeColumn(3); });
                    foreach (var (item, detail, amount) in lines)
                    {
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(item);
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(detail).FontSize(9);
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).AlignRight().Text(Fmt.Vnd(amount));
                    }
                    t.Cell().ColumnSpan(2).PaddingTop(4).Text("TỔNG CỘNG").Bold();
                    t.Cell().PaddingTop(4).AlignRight().Text(Fmt.Vnd(bill.TotalAmount)).Bold();
                    t.Cell().ColumnSpan(2).Text("Đã thu");
                    t.Cell().AlignRight().Text(Fmt.Vnd(paid));
                    t.Cell().ColumnSpan(2).Text("Còn lại").Bold();
                    t.Cell().AlignRight().Text(Fmt.Vnd(Math.Max(0, bill.TotalAmount - paid))).Bold();
                });
                col.Item().Text($"Bằng chữ: {VietnameseNumber.ToWords(bill.TotalAmount)}").Italic().FontSize(10);
                if (!string.IsNullOrWhiteSpace(bill.Note)) col.Item().Text($"Ghi chú: {bill.Note}").FontSize(10);
                foreach (var p in bill.Payments.OrderBy(p => p.Date))
                    col.Item().Text($"• Thu {Fmt.Date(p.Date)}: {Fmt.Vnd(p.Amount)} ({p.Account.Name})").FontSize(9);
                col.Item().PaddingTop(4).Text($"Hạn đóng kỳ sau: ngày {s.BillingDay:00} hằng tháng. Điện {Fmt.Money(s.ElectricityPrice)}đ/số, nước {Fmt.Money(s.WaterPricePerPerson)}đ/người/tháng.").FontSize(9);
                col.Item().PaddingTop(18).Row(r =>
                {
                    r.RelativeItem().AlignCenter().Text("Người nộp tiền").Bold();
                    r.RelativeItem().AlignCenter().Text("Người thu tiền").Bold();
                });
            });
            page.Footer().AlignCenter().Text($"{s.Name}{(string.IsNullOrWhiteSpace(s.Phone) ? "" : " - ĐT: " + s.Phone)}").FontSize(8).FontColor(Colors.Grey.Darken1);
        }));
        return (doc.GeneratePdf(), $"PhieuThu_{bill.Lease.Room.Code}_{bill.Month:00}{bill.Year}_{bill.Id}.pdf");
    }

    public async Task<(byte[] Bytes, string FileName)?> CheckoutPdfAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var lease = await db.Leases.AsNoTracking().Include(l => l.Room).Include(l => l.Penalties)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant).AsSplitQuery().FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null || lease.Status != LeaseStatus.Ended) return null;
        var rep = lease.Tenants.OrderByDescending(t => t.IsRepresentative).Select(t => t.Tenant.FullName).FirstOrDefault() ?? "";

        var doc = Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(Base.FontSize(11));
            page.Content().Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(s.Name.ToUpper(Fmt.Vi)).Bold();
                col.Item().PaddingTop(6).AlignCenter().Text($"BIÊN BẢN TRẢ PHÒNG {lease.Room.Code}").FontSize(15).Bold();
                col.Item().Text($"Người thuê: {rep}");
                col.Item().Text($"Thời gian thuê: {Fmt.Date(lease.StartDate)} - {Fmt.Date(lease.EndDate)}");
                col.Item().Text($"Số điện: đầu {lease.StartMeterReading}, cuối {lease.EndMeterReading}");
                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(6); cd.RelativeColumn(3); });
                    void Row(string a, decimal v, bool bold = false)
                    {
                        var x = t.Cell().PaddingVertical(2).Text(a); var y = t.Cell().PaddingVertical(2).AlignRight().Text(Fmt.Vnd(v));
                        if (bold) { x.Bold(); y.Bold(); }
                    }
                    Row("Tiền cọc", lease.Deposit);
                    Row("Trừ tiền điện chưa thu", -(lease.CheckoutElectricity ?? 0));
                    foreach (var p in lease.Penalties) Row("Trừ phạt: " + p.Description, -p.Amount);
                    if ((lease.CheckoutOther ?? 0) != 0) Row("Khoản khác", -(lease.CheckoutOther ?? 0));
                    Row("Đã trả lại khách", lease.DepositRefunded ?? 0, true);
                    if ((lease.CheckoutCollected ?? 0) > 0) Row("Khách nộp thêm", lease.CheckoutCollected ?? 0, true);
                });
                if (!string.IsNullOrWhiteSpace(lease.FurnitureWarnings)) col.Item().PaddingTop(4).Text("Đồ đạc: " + lease.FurnitureWarnings).FontSize(10);
                if (!string.IsNullOrWhiteSpace(lease.CheckoutNote)) col.Item().Text("Ghi chú: " + lease.CheckoutNote).FontSize(10);
                col.Item().PaddingTop(18).Row(r =>
                {
                    r.RelativeItem().AlignCenter().Text("Người thuê").Bold();
                    r.RelativeItem().AlignCenter().Text("Chủ nhà").Bold();
                });
            });
        }));
        return (doc.GeneratePdf(), $"TraPhong_{lease.Room.Code}_{lease.EndDate:yyyyMMdd}.pdf");
    }

    // ============================ HỢP ĐỒNG (WORD) ============================

    public string ContractTemplatePath => Path.Combine(env.ContentRootPath, "Templates", "HOPDONG.docx");

    public async Task<(byte[] Bytes, string FileName)?> ContractDocxAsync(int leaseId)
    {
        if (!File.Exists(ContractTemplatePath)) throw new InvalidOperationException("Không tìm thấy file mẫu hợp đồng (Templates/HOPDONG.docx).");
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var lease = await db.Leases.AsNoTracking().Include(l => l.Room).ThenInclude(r => r.Furniture).ThenInclude(f => f.FurnitureItem)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant).AsSplitQuery().FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null) return null;

        var tenants = lease.Tenants.Where(t => t.MoveOutDate == null || lease.Status == LeaseStatus.Ended)
            .OrderByDescending(t => t.IsRepresentative).ThenBy(t => t.MoveInDate).Select(t => t.Tenant).ToList();
        var sb = new StringBuilder();
        if (tenants.Count > 0)
        {
            var m = tenants[0];
            sb.AppendLine($"Họ và tên: {m.FullName}");
            sb.AppendLine($"CCCD/Hộ chiếu: {m.IdentityCard}" + (m.BirthDate.HasValue ? $"; Ngày sinh: {m.BirthDate:dd/MM/yyyy}" : ""));
            sb.AppendLine($"Điện thoại: {m.Phone}");
            sb.AppendLine($"Địa chỉ thường trú: {m.PermanentAddress}");
            if (tenants.Count > 1)
            {
                sb.AppendLine("Cùng ở với:");
                for (int i = 1; i < tenants.Count; i++) sb.AppendLine($"{i}. {tenants[i].FullName} - CCCD: {tenants[i].IdentityCard}");
            }
        }
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ngay"] = lease.StartDate.Day.ToString(), ["thang"] = lease.StartDate.Month.ToString(), ["nam"] = lease.StartDate.Year.ToString(),
            ["sophong"] = lease.Room.Code, ["benthue"] = sb.ToString().TrimEnd(),
            ["giathue"] = Fmt.Money(lease.MonthlyRent), ["giathue_text"] = VietnameseNumber.ToWords(lease.MonthlyRent),
            ["tiencoc"] = Fmt.Money(lease.Deposit), ["tiencoc_text"] = VietnameseNumber.ToWords(lease.Deposit),
            ["giadien"] = Fmt.Money(s.ElectricityPrice), ["gianuoc"] = Fmt.Money(s.WaterPricePerPerson),
            ["ngaybatdau"] = lease.StartDate.ToString("dd/MM/yyyy"), ["sodiendau"] = lease.StartMeterReading.ToString(),
            ["songuoi"] = tenants.Count.ToString(), ["ngaythu"] = s.BillingDay.ToString(),
            ["dodac"] = string.Join(", ", lease.Room.Furniture.OrderBy(f => f.FurnitureItem.SortOrder).Select(f => $"{f.FurnitureItem.Name} ({f.Quantity})")),
            ["chunha"] = s.Owner ?? "", ["diachi"] = s.Address ?? "", ["dienthoai"] = s.Phone ?? ""
        };

        // Mẫu cũ ghi cứng một số con số: thay bằng số liệu của hợp đồng.
        var literals = new Dictionary<string, string>
        {
            ["Kể từ ngày …/…/2025"] = $"Kể từ ngày {lease.StartDate:dd/MM/yyyy}",
            ["đến hết ngày …/…/2025"] = "đến hết ngày …/…/……",
            ["[…………………VNĐ/tháng ]"] = $"[{Fmt.Money(lease.MonthlyRent)} VNĐ/tháng ({VietnameseNumber.ToWords(lease.MonthlyRent)})]",
            ["đến ngày mồng 10 tháng sau"] = $"đến ngày mồng {s.BillingDay} tháng sau",
            ["Tiền điện:  3.500Đ/Kw sử dụng"] = $"Tiền điện: {Fmt.Money(s.ElectricityPrice)}Đ/kWh sử dụng",
            ["Tiền nước: 15.000Đ/m3sử dụng"] = $"Tiền nước: {Fmt.Money(s.WaterPricePerPerson)}Đ/người/tháng",
            ["[2.000.000Đ (hai triệu đồng)]"] = $"[{Fmt.Money(lease.Deposit)}Đ ({VietnameseNumber.ToWords(lease.Deposit)})]",
        };

        using var ms = new MemoryStream();
        await using (var fs = File.OpenRead(ContractTemplatePath)) await fs.CopyToAsync(ms);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var main = doc.MainDocumentPart;
            var parts = new List<OpenXmlPartRootElement?> { main?.Document };
            if (main != null)
            {
                parts.AddRange(main.HeaderParts.Select(h => (OpenXmlPartRootElement)h.Header));
                parts.AddRange(main.FooterParts.Select(f => (OpenXmlPartRootElement)f.Footer));
            }
            foreach (var root in parts.OfType<OpenXmlPartRootElement>())
            {
                MergeFields(root, data);
                ReplaceText(root, literals);
                root.Save();
            }
        }
        return (ms.ToArray(), $"HopDong_{lease.Room.Code}_{lease.StartDate:yyyyMMdd}.docx");
    }

    private static void MergeFields(OpenXmlElement root, Dictionary<string, string> data)
    {
        foreach (var simple in root.Descendants<SimpleField>().ToList())
        {
            var name = FieldName(simple.Instruction?.Value);
            if (name == null || !data.TryGetValue(name, out var value)) continue;
            var props = simple.Descendants<RunProperties>().FirstOrDefault()?.CloneNode(true) as RunProperties;
            simple.Parent?.ReplaceChild(BuildRun(value, props), simple);
        }

        static bool IsChar(Run r, FieldCharValues type) => r.GetFirstChild<FieldChar>()?.FieldCharType?.Value == type;
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            int searchFrom = 0;
            while (true)
            {
                var runs = paragraph.Elements<Run>().ToList();
                if (searchFrom >= runs.Count) break;
                int begin = runs.FindIndex(searchFrom, r => IsChar(r, FieldCharValues.Begin));
                if (begin < 0) break;
                int end = runs.FindIndex(begin, r => IsChar(r, FieldCharValues.End));
                if (end < 0) break;
                int sep = runs.FindIndex(begin, end - begin, r => IsChar(r, FieldCharValues.Separate));
                var instr = string.Concat(runs.Skip(begin).Take((sep >= 0 ? sep : end) - begin).SelectMany(r => r.Elements<FieldCode>()).Select(fc => fc.Text));
                var name = FieldName(instr);
                if (name == null || !data.TryGetValue(name, out var value)) { searchFrom = end + 1; continue; }
                var resultRun = sep >= 0 ? runs.Skip(sep + 1).Take(end - sep - 1).FirstOrDefault(r => r.GetFirstChild<Text>() != null) : null;
                var props = (resultRun ?? runs[begin]).RunProperties?.CloneNode(true) as RunProperties;
                runs[begin].InsertBeforeSelf(BuildRun(value, props));
                for (int i = begin; i <= end; i++) runs[i].Remove();
                searchFrom = begin + 1;
            }
        }
    }

    /// <summary>Thay chữ trong từng đoạn văn, kể cả khi chữ bị tách qua nhiều run; giữ định dạng các phần khác của đoạn.</summary>
    private static void ReplaceText(OpenXmlElement root, Dictionary<string, string> replacements)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            foreach (var (find, replace) in replacements)
            {
                int startAt = 0;
                while (true)
                {
                    var texts = paragraph.Descendants<Text>().ToList();
                    var full = string.Concat(texts.Select(t => t.Text));
                    int idx = startAt <= full.Length ? full.IndexOf(find, startAt, StringComparison.Ordinal) : -1;
                    if (idx < 0) break;
                    startAt = idx + replace.Length;
                    int end = idx + find.Length, pos = 0;
                    bool placed = false;
                    foreach (var t in texts)
                    {
                        int tStart = pos, tEnd = pos + t.Text.Length;
                        pos = tEnd;
                        if (tEnd <= idx || tStart >= end) continue;
                        var before = idx > tStart ? t.Text[..(idx - tStart)] : "";
                        var after = end < tEnd ? t.Text[(end - tStart)..] : "";
                        t.Text = before + (placed ? "" : replace) + after;
                        t.Space = SpaceProcessingModeValues.Preserve;
                        placed = true;
                    }
                }
            }
        }
    }

    private static string? FieldName(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var parts = instruction.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0].Equals("MERGEFIELD", StringComparison.OrdinalIgnoreCase) ? parts[1].Trim('"') : null;
    }

    private static Run BuildRun(string value, RunProperties? props)
    {
        var run = new Run();
        if (props != null) run.Append(props);
        var lines = value.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    // ============================ EXCEL ============================

    /// <summary>Sổ đăng ký lưu trú: sheet người Việt Nam và sheet người nước ngoài.</summary>
    public async Task<byte[]> ResidentRegisterAsync(List<ResidentRow> rows)
    {
        var s = await GetSettingsAsync();
        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Times New Roman";
        var vn = rows.Where(r => !Nationality.IsForeign(r.Nationality, r.Ethnicity)).ToList();
        var fo = rows.Where(r => Nationality.IsForeign(r.Nationality, r.Ethnicity)).ToList();
        var title = $"DANH SÁCH ĐĂNG KÝ LƯU TRÚ TẠI {s.Name.ToUpper(Fmt.Vi)}" + (string.IsNullOrWhiteSpace(s.RegisterArea) ? "" : ", " + s.RegisterArea.ToUpper(Fmt.Vi));
        Register(wb.Worksheets.Add("Việt Nam"), vn, title,
            ["STT", "Phòng", "Họ và tên", "Ngày sinh", "Giới tính", "Số giấy tờ", "Dân tộc", "Điện thoại", "Địa chỉ thường trú", "Biển số xe", "Ngày đến"],
            [6, 9, 24, 12, 8, 16, 9, 13, 45, 12, 12],
            r => [r.RoomCode, r.FullName, Fmt.Date(r.BirthDate), r.Gender ?? "", r.IdentityCard, r.Ethnicity ?? "Kinh", r.Phone ?? "", r.PermanentAddress ?? "", r.VehiclePlate ?? "", Fmt.Date(r.MoveInDate)]);
        var wf = wb.Worksheets.Add("Nước ngoài");
        if (fo.Count > 0)
            Register(wf, fo, $"DANH SÁCH NGƯỜI NƯỚC NGOÀI LƯU TRÚ TẠI {s.Name.ToUpper(Fmt.Vi)}",
                ["STT", "Phòng", "Họ và tên", "Ngày sinh", "Giới tính", "Số hộ chiếu/giấy tờ", "Quốc tịch", "Địa chỉ", "Ngày đến"],
                [6, 9, 24, 12, 8, 18, 12, 40, 12],
                r => [r.RoomCode, r.FullName, Fmt.Date(r.BirthDate), r.Gender ?? "", r.IdentityCard, r.Nationality ?? r.Ethnicity ?? "", r.PermanentAddress ?? "", Fmt.Date(r.MoveInDate)]);
        else wf.Cell(1, 1).Value = "Không có người nước ngoài";
        return Save(wb);
    }

    private static void Register(IXLWorksheet ws, List<ResidentRow> rows, string title, string[] headers, double[] widths, Func<ResidentRow, string[]> values)
    {
        int cols = headers.Length;
        var t = ws.Range(1, 1, 2, cols).Merge();
        t.Value = title; t.Style.Font.Bold = true; t.Style.Alignment.WrapText = true;
        t.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; t.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        var d = ws.Range(3, 1, 3, cols).Merge(); d.Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}"; d.Style.Font.Italic = true;
        for (int i = 0; i < cols; i++) ws.Cell(4, i + 1).Value = headers[i];
        var h = ws.Range(4, 1, 4, cols);
        h.Style.Font.Bold = true; h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; h.Style.Alignment.WrapText = true;
        int row = 5, stt = 1;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = stt++;
            var v = values(r);
            for (int i = 0; i < v.Length; i++) ws.Cell(row, i + 2).Value = v[i];
            row++;
        }
        var all = ws.Range(4, 1, Math.Max(4, row - 1), cols);
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin; all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        all.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center; all.Style.Alignment.WrapText = true;
        ws.Cell(row + 1, 1).Value = $"Tổng số người: {rows.Count} - Số phòng: {rows.Select(r => r.RoomCode).Distinct().Count()}";
        ws.Cell(row + 1, 1).Style.Font.Bold = true;
        for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.SheetView.FreezeRows(4);
    }

    public async Task<byte[]> LedgerAsync(LedgerFilter filter)
    {
        filter.Page = 1;
        filter.PageSize = 200;
        var rows = new List<LedgerRow>();
        while (true)
        {
            var page = await accounts.GetLedgerAsync(filter);
            rows.AddRange(page.Rows);
            if (rows.Count >= page.Total || page.Rows.Count == 0) break;
            filter.Page++;
        }
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sổ thu chi");
        string[] headers = ["Ngày", "Tài khoản", "Loại", "Phòng", "Nội dung", "Thu", "Chi", "Người ghi"];
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        int r = 2;
        foreach (var x in rows.OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            ws.Cell(r, 1).Value = x.Date; ws.Cell(r, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(r, 2).Value = x.AccountName; ws.Cell(r, 3).Value = TxCategory.Display(x.Category); ws.Cell(r, 4).Value = x.RoomCode;
            ws.Cell(r, 5).Value = x.Description;
            if (x.Amount > 0) ws.Cell(r, 6).Value = x.Amount; else ws.Cell(r, 7).Value = -x.Amount;
            ws.Cell(r, 8).Value = x.CreatedBy;
            r++;
        }
        ws.Range(2, 6, Math.Max(2, r), 7).Style.NumberFormat.Format = "#,##0";
        ws.Cell(r, 5).Value = "Tổng"; ws.Cell(r, 5).Style.Font.Bold = true;
        ws.Cell(r, 6).FormulaA1 = $"SUM(F2:F{r - 1})"; ws.Cell(r, 7).FormulaA1 = $"SUM(G2:G{r - 1})";
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task<AppSetting> GetSettingsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Settings.AsNoTracking().FirstAsync();
    }

    // ============================ MÃ QR ============================

    public static byte[] QrPng(string text, int pixelsPerModule = 12)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }
}
