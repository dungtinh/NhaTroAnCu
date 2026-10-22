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
            .Include(b => b.Lease).ThenInclude(l => l.CompanyContract)
            .AsSplitQuery().FirstOrDefaultAsync(b => b.Id == billId);
        if (bill == null) return null;
        var company = bill.Lease.CompanyContract;
        var rep = company != null ? $"{company.CompanyName} (phòng {bill.Lease.Room.Code})"
            : bill.Lease.Tenants.OrderByDescending(t => t.IsRepresentative).Select(t => t.Tenant.FullName).FirstOrDefault() ?? "";
        decimal paid = bill.Payments.Sum(p => p.Amount);
        var lines = new List<(string Item, string Detail, decimal Amount)>();
        if (bill.RentAmount != 0)
            lines.Add(("Tiền phòng", bill.Kind switch
            {
                BillKind.MoveIn => $"1 tháng trả trước, kỳ {bill.PeriodFrom:dd/MM/yyyy} - {bill.PeriodTo:dd/MM/yyyy}",
                BillKind.MoveOut => $"Kỳ chưa lập phiếu, tính đủ tháng ({bill.PeriodFrom:dd/MM/yyyy} - {bill.PeriodTo:dd/MM/yyyy})",
                BillKind.ContractRent => CompanyService.ContractRentDetail(bill, bill.Lease.MonthlyRent) + "đ",
                _ => LeaseService.IsStubBill(bill.PeriodFrom, bill.PeriodTo, bill.Lease.StartDate, s.BillingDay)
                    ? $"Kỳ đầu từ ngày vào {bill.PeriodFrom:dd/MM/yyyy} đến ngày chốt {bill.PeriodTo:dd/MM/yyyy}: {LeaseService.CalculateStubPeriod(0, bill.Lease.StartDate, s.BillingDay).Days} ngày × {Fmt.Money(bill.Lease.MonthlyRent)}đ ÷ 30"
                    : $"Tròn tháng {bill.PeriodFrom:dd/MM/yyyy} - {bill.PeriodTo:dd/MM/yyyy}"
            }, bill.RentAmount));
        if (bill.ElectricityKwh != 0 || bill.ElectricityAmount != 0)
            lines.Add(("Tiền điện", $"{bill.ElectricityKwh} số × {Fmt.Money(s.ElectricityPrice)}đ", bill.ElectricityAmount));
        if (bill.WaterAmount != 0) lines.Add(("Tiền nước", bill.WaterAmount != bill.PersonCount * s.WaterPricePerPerson && company is { WaterPerRoom: > 0 }
            ? $"Cố định {Fmt.Money(company.WaterPerRoom)}đ/phòng/tháng theo hợp đồng" : $"{bill.PersonCount} người × {Fmt.Money(s.WaterPricePerPerson)}đ", bill.WaterAmount));
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
                col.Item().PaddingTop(4).Text(company != null
                    ? $"Theo hợp đồng {company.ContractNo ?? company.ShortName}: tiền phòng nộp theo kỳ {company.RentMonthsPerPeriod} tháng; điện {Fmt.Money(s.ElectricityPrice)}đ/số, nước {Fmt.Money(company.WaterPerRoom)}đ/phòng thu hằng tháng ngày {s.BillingDay:00}."
                    : $"Hạn đóng kỳ sau: ngày {s.BillingDay:00} hằng tháng. Điện {Fmt.Money(s.ElectricityPrice)}đ/số, nước {Fmt.Money(s.WaterPricePerPerson)}đ/người/tháng.").FontSize(9);
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
        var moveOut = await db.Bills.AsNoTracking().Where(b => b.LeaseId == leaseId && b.Kind == BillKind.MoveOut).OrderByDescending(b => b.Id).FirstOrDefaultAsync();

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
                if (moveOut != null && moveOut.ElectricityKwh > 0) col.Item().Text($"Điện kỳ cuối: {moveOut.ElectricityKwh} số × {Fmt.Money(s.ElectricityPrice)}đ");
                col.Item().PaddingTop(4).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(6); cd.RelativeColumn(3); });
                    void Row(string a, decimal v, bool bold = false)
                    {
                        var x = t.Cell().PaddingVertical(2).Text(a); var y = t.Cell().PaddingVertical(2).AlignRight().Text(Fmt.Vnd(v));
                        if (bold) { x.Bold(); y.Bold(); }
                    }
                    Row("Tiền cọc", lease.Deposit);
                    Row("Trừ tiền điện kỳ cuối", -(lease.CheckoutElectricity ?? 0));
                    if (moveOut != null && moveOut.RentAmount != 0) Row($"Trừ tiền phòng kỳ chưa lập phiếu (đủ tháng)", -moveOut.RentAmount);
                    if (moveOut != null && moveOut.WaterAmount != 0) Row("Trừ tiền nước kỳ chưa lập phiếu", -moveOut.WaterAmount);
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

    /// <summary>
    /// Điền hợp đồng từ mẫu Templates/HOPDONG.docx. Chỉ điền phần của khách: số phòng, họ tên / CCCD / điện thoại
    /// từng người ở, ngày ký và thời hạn thuê; giá thuê, phí, bên cho thuê... giữ nguyên như mẫu.
    /// </summary>
    public async Task<(byte[] Bytes, string FileName)?> ContractDocxAsync(int leaseId)
    {
        if (!File.Exists(ContractTemplatePath)) throw new InvalidOperationException("Không tìm thấy file mẫu hợp đồng (Templates/HOPDONG.docx).");
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var lease = await db.Leases.AsNoTracking().Include(l => l.Room)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant).AsSplitQuery().FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null) return null;

        var tenants = lease.Tenants.Where(t => t.MoveOutDate == null || lease.Status == LeaseStatus.Ended)
            .OrderByDescending(t => t.IsRepresentative).ThenBy(t => t.MoveInDate).Select(t => t.Tenant).ToList();
        var start = lease.StartDate.Date;
        var termEnd = start.AddMonths(Math.Max(1, s.MinStayMonthsForDepositRefund)).AddDays(-1);

        // Các đoạn chấm chấm trong mẫu -> số liệu của hợp đồng (chữ trong mẫu bị tách nhiều run nên tìm theo cả đoạn văn).
        var literals = new Dictionary<string, string>
        {
            ["PHÒNG SỐ: …….."] = $"PHÒNG SỐ: {lease.Room.Code}",
            ["Phòng số […………]"] = $"Phòng số [{lease.Room.Code}]",
            ["ngày …. tháng …. năm 2026"] = $"ngày {start.Day:00} tháng {start.Month:00} năm {start.Year}",
            ["Kể từ ngày …/…/2026 đến …/…/2026"] = $"Kể từ ngày {start:dd/MM/yyyy} đến {termEnd:dd/MM/yyyy}",
        };

        using var ms = new MemoryStream();
        await using (var fs = File.OpenRead(ContractTemplatePath)) await fs.CopyToAsync(ms);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart?.Document.Body ?? throw new InvalidOperationException("File mẫu hợp đồng không hợp lệ.");
            FillTenantLines(body, tenants);
            ReplaceText(body, literals);
            doc.MainDocumentPart!.Document.Save();
        }
        return (ms.ToArray(), $"HopDong_{lease.Room.Code}_{lease.StartDate:yyyyMMdd}.docx");
    }

    /// <summary>
    /// Mẫu có sẵn các dòng "Họ và tên: … /CCCD: […]/Đt[…]" dưới BÊN THUÊ. Mỗi người ở điền một dòng (thiếu dòng thì nhân bản dòng cuối);
    /// dòng thừa giữ nguyên chấm chấm để viết tay. Thông tin chưa có (CCCD tạm CHUA-CO, không số điện thoại) cũng để chấm chấm.
    /// </summary>
    private static void FillTenantLines(Body body, List<Tenant> tenants)
    {
        var lines = body.Descendants<Paragraph>()
            .Where(p => { var t = p.InnerText.TrimStart(); return t.StartsWith("Họ và tên:", StringComparison.Ordinal) && t.Contains("CCCD", StringComparison.Ordinal); })
            .ToList();
        if (lines.Count == 0) return;
        while (lines.Count < tenants.Count)
        {
            var clone = (Paragraph)lines[^1].CloneNode(true);
            // Word yêu cầu paraId/textId duy nhất trong tài liệu: bỏ ở bản sao để Word tự cấp lại.
            foreach (var a in clone.GetAttributes().Where(a => a.LocalName is "paraId" or "textId").ToList()) clone.RemoveAttribute(a.LocalName, a.NamespaceUri);
            lines[^1].InsertAfterSelf(clone);
            lines.Add(clone);
        }
        for (int i = 0; i < tenants.Count; i++)
        {
            var t = tenants[i];
            var card = string.IsNullOrWhiteSpace(t.IdentityCard) || t.IdentityCard.StartsWith("CHUA-CO", StringComparison.OrdinalIgnoreCase) ? "………………………" : t.IdentityCard.Trim();
            var phone = string.IsNullOrWhiteSpace(t.Phone) ? "………………." : t.Phone.Trim();
            SetParagraphText(lines[i], $"Họ và tên: {t.FullName.Trim()} /CCCD: [{card}]/Đt[{phone}]");
        }
    }

    /// <summary>Thay toàn bộ chữ của đoạn văn, giữ định dạng của run đầu tiên.</summary>
    private static void SetParagraphText(Paragraph p, string text)
    {
        var runs = p.Elements<Run>().ToList();
        var props = runs.FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var r in runs) r.Remove();
        var run = new Run();
        if (props != null) run.Append(props);
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        p.Append(run);
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

    // ============================ SỐ ĐIỆN (EXCEL) ============================

    private const string MeterRoomHeader = "Phòng";
    private const string MeterKwhHeader = "Số điện (kWh)";

    /// <summary>File mẫu ghi số điện: mỗi phòng đang có khách một dòng, điền cột "Số điện (kWh)" rồi nhập lại.</summary>
    public static byte[] MeterTemplate(BillingSheet sheet)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Số điện");
        ws.Cell(1, 1).Value = $"SỐ ĐIỆN ĐÃ DÙNG KỲ CHỐT {sheet.DueDate:dd/MM/yyyy}";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = "Chỉ điền cột Số điện (kWh). Không đổi tên cột, không sửa mã phòng.";
        ws.Cell(2, 1).Style.Font.Italic = true;
        string[] headers = ["STT", MeterRoomHeader, "Người thuê", "Số người", MeterKwhHeader];
        for (int i = 0; i < headers.Length; i++) ws.Cell(4, i + 1).Value = headers[i];
        var head = ws.Range(4, 1, 4, headers.Length);
        head.Style.Font.Bold = true; head.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        int r = 5, n = 1;
        foreach (var x in sheet.Rows.Where(x => !x.SkipThisMonth).OrderBy(x => x.BlockCode).ThenBy(x => x.Floor).ThenBy(x => x.Number))
        {
            ws.Cell(r, 1).Value = n++;
            ws.Cell(r, 2).Value = x.RoomCode;
            ws.Cell(r, 3).Value = x.Representative;
            ws.Cell(r, 4).Value = x.PersonCount;
            if (x.Kwh is int k) ws.Cell(r, 5).Value = k;
            ws.Cell(r, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
            r++;
        }
        ws.Range(4, 1, Math.Max(4, r - 1), headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Range(4, 1, Math.Max(4, r - 1), headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Column(1).Width = 6; ws.Column(2).Width = 10; ws.Column(3).Width = 28; ws.Column(4).Width = 10; ws.Column(5).Width = 16;
        ws.SheetView.FreezeRows(4);
        return Save(wb);
    }

    /// <summary>
    /// Đọc file Excel số điện: tìm dòng tiêu đề có cột "Phòng" và cột số điện ("Số điện"/"kWh"), trả về số điện theo mã phòng.
    /// Không có tiêu đề thì lấy cột A là mã phòng, cột số cuối cùng là số điện.
    /// </summary>
    public static (Dictionary<string, int> Values, List<string> Errors) ParseMeterSheet(Stream stream)
    {
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        XLWorkbook wb;
        try { wb = new XLWorkbook(stream); }
        catch (Exception) { throw new InvalidOperationException("Không đọc được file. Hãy dùng file Excel .xlsx (tải file mẫu để điền)."); }
        using (wb)
        {
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            if (used == null) throw new InvalidOperationException("File trống.");
            int lastRow = used.LastRow().RowNumber(), lastCol = used.LastColumn().ColumnNumber();

            static string Norm(string s) => s.Trim().ToLowerInvariant();
            int headerRow = 0, roomCol = 1, kwhCol = 0;
            for (int r = 1; r <= Math.Min(lastRow, 15) && headerRow == 0; r++)
                for (int c = 1; c <= lastCol; c++)
                {
                    var t = Norm(ws.Cell(r, c).GetString());
                    if (t is "phòng" or "phong" or "mã phòng" or "ma phong") { headerRow = r; roomCol = c; }
                }
            if (headerRow > 0)
                for (int c = 1; c <= lastCol; c++)
                {
                    var t = Norm(ws.Cell(headerRow, c).GetString());
                    if (t.Contains("kwh") || t.Contains("số điện") || t.Contains("so dien")) kwhCol = c;
                }
            if (kwhCol == 0) kwhCol = lastCol;

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                var code = ws.Cell(r, roomCol).GetString().Trim().ToUpperInvariant().Replace(" ", "");
                if (code.Length == 0) continue;
                var cell = ws.Cell(r, kwhCol);
                if (cell.IsEmpty()) continue;
                if (cell.TryGetValue<double>(out var d) && d >= 0 && d < 1_000_000 && Math.Abs(d - Math.Round(d)) < 0.001)
                    values[code] = (int)Math.Round(d);
                else if (int.TryParse(new string(cell.GetString().Where(char.IsDigit).ToArray()), out var k) && cell.GetString().Trim().Length > 0)
                    values[code] = k;
                else
                    errors.Add($"Dòng {r} ({code}): số điện \"{cell.GetString()}\" không hợp lệ.");
            }
        }
        return (values, errors);
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
