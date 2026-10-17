using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NHNhaTro.Services;

/// <summary>Thông tin nhà trọ in trên chứng từ (cấu hình mục "NhaTro" trong appsettings).</summary>
public class LandlordOptions
{
    public string Name { get; set; } = "NHÀ TRỌ AN CƯ";
    public string Address { get; set; } = "";
    /// <summary>Địa bàn in trên tiêu đề sổ đăng ký lưu trú (ví dụ "Tổ dân phố ..., phường ...").</summary>
    public string RegisterArea { get; set; } = "";
    public string Owner { get; set; } = "";
    public string OwnerIdentityCard { get; set; } = "";
    public string Phone { get; set; } = "";
    /// <summary>Ngày hạn đóng tiền hằng tháng (in trên hóa đơn).</summary>
    public int PaymentDueDay { get; set; } = 10;
}

/// <summary>Tạo PDF bằng QuestPDF (giấy phép Community, miễn phí cho doanh nghiệp doanh thu &lt; 1 triệu USD/năm).</summary>
public class PdfDocuments(IDbContextFactory<ApplicationDbContext> dbFactory, Microsoft.Extensions.Options.IOptions<LandlordOptions> landlord)
{
    public const string FontFamily = "Times New Roman";
    private LandlordOptions L => landlord.Value;

    private static TextStyle Base => TextStyle.Default.FontFamily(FontFamily, "Arial", "Lato").FontSize(12);

    public async Task<(byte[] Bytes, string FileName)?> UtilityBillAsync(int billId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var bill = await db.UtilityBills.AsNoTracking()
            .Include(b => b.Room)
            .Include(b => b.Contract).ThenInclude(c => c.Company)
            .Include(b => b.Contract).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == billId);
        if (bill == null) return null;

        string tenantName = bill.Contract.IsCompany && bill.Contract.Company != null
            ? bill.Contract.Company.CompanyName
            : bill.Contract.ContractTenants.Where(ct => ct.RoomId == bill.RoomId).OrderBy(ct => ct.CreatedAt).Select(ct => ct.Tenant.FullName).FirstOrDefault()
              ?? bill.Contract.ContractTenants.OrderBy(ct => ct.CreatedAt).Select(ct => ct.Tenant.FullName).FirstOrDefault() ?? "";
        int waterUsed = bill.WaterIndexEnd - bill.WaterIndexStart;

        var doc = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(Base);

            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text("PHIẾU BÁO TIỀN THUÊ PHÒNG").FontSize(18).Bold();
                col.Item().AlignCenter().Text($"Tháng {bill.Month}/{bill.Year}").FontSize(14).Bold();
                col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            });

            page.Content().PaddingVertical(15).Column(col =>
            {
                col.Spacing(6);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Phòng: {bill.Room.Name}").Bold();
                        c.Item().Text($"Mã hợp đồng: #{bill.ContractId}");
                        c.Item().Text($"Khách thuê: {tenantName}");
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Ngày lập: {DateTime.Today:dd/MM/yyyy}");
                        c.Item().Text($"Mã phiếu: #PBT{bill.Id:D6}");
                        c.Item().Text($"Trạng thái: {(bill.BillStatus == "Final" ? "Chính thức" : "Nháp")}");
                    });
                });

                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(40);
                        c.RelativeColumn(3);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        foreach (var t in new[] { "STT", "Khoản mục", "Chi tiết", "Thành tiền" })
                            h.Cell().Background("#3498db").Padding(6).AlignCenter().Text(t).FontColor(Colors.White).Bold();
                    });

                    int stt = 1;
                    void Line(string item, string detail, string amount)
                    {
                        table.Cell().Border(0.5f).Padding(5).AlignCenter().Text((stt++).ToString());
                        table.Cell().Border(0.5f).Padding(5).Text(item);
                        table.Cell().Border(0.5f).Padding(5).AlignCenter().Text(detail);
                        table.Cell().Border(0.5f).Padding(5).AlignRight().Text(amount);
                    }

                    Line("Tiền thuê phòng", $"Tháng {bill.Month}/{bill.Year}", Fmt.Vnd(bill.RentAmount));
                    Line("Tiền điện", $"{Fmt.Money(bill.Contract.ElectricityPrice)}đ/kWh", Fmt.Vnd(bill.ElectricityAmount));
                    Line("Tiền nước", $"{waterUsed}m³ × {Fmt.Money(bill.Contract.WaterPrice)}đ", Fmt.Vnd(bill.WaterAmount));
                    if (bill.ExtraCharge > 0) Line("Phụ thu", "", Fmt.Vnd(bill.ExtraCharge));
                    if (bill.Discount > 0) Line("Giảm trừ", "", "-" + Fmt.Vnd(bill.Discount));

                    table.Cell().ColumnSpan(3).Border(0.5f).Background(Colors.Grey.Lighten3).Padding(7).AlignRight().Text("TỔNG CỘNG").Bold();
                    table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(7).AlignRight().Text(Fmt.Vnd(bill.TotalAmount)).Bold();
                });

                col.Item().PaddingTop(8).Text($"Chỉ số nước: Đầu kỳ {bill.WaterIndexStart} - Cuối kỳ {bill.WaterIndexEnd} - Tiêu thụ {waterUsed}m³");
                if (!string.IsNullOrWhiteSpace(bill.BillNote))
                    col.Item().Text(t => { t.Span("Ghi chú: ").Bold(); t.Span(bill.BillNote); });
                col.Item().PaddingTop(6).Text($"Số tiền bằng chữ: {VietnameseNumber.ToWords(bill.TotalAmount)}").Bold();

                col.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Column(c => { c.Item().AlignCenter().Text("Người thuê").Bold(); c.Item().AlignCenter().Text("(Ký và ghi rõ họ tên)").FontSize(10).FontColor(Colors.Grey.Darken1); });
                    row.RelativeItem().AlignCenter().Column(c => { c.Item().AlignCenter().Text("Người cho thuê").Bold(); c.Item().AlignCenter().Text("(Ký và ghi rõ họ tên)").FontSize(10).FontColor(Colors.Grey.Darken1); });
                });
            });

            page.Footer().AlignCenter().Text(FooterText()).FontSize(10).FontColor(Colors.Grey.Darken1);
        }));

        return (doc.GeneratePdf(), $"PhieuBaoTien_Phong{bill.Room.Name}_T{bill.Month}.{bill.Year}.pdf");
    }

    public async Task<(byte[] Bytes, string FileName)?> PaymentReceiptAsync(int incomeExpenseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var payment = await db.IncomeExpenses.AsNoTracking()
            .Include(ie => ie.Contract!).ThenInclude(c => c.ContractRooms).ThenInclude(cr => cr.Room)
            .Include(ie => ie.Contract!).ThenInclude(c => c.ContractTenants).ThenInclude(ct => ct.Tenant)
            .Include(ie => ie.Contract!).ThenInclude(c => c.Company)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ie => ie.Id == incomeExpenseId);
        if (payment?.Contract == null) return null;
        var contract = payment.Contract;

        int month = payment.TransactionDate.Month, year = payment.TransactionDate.Year;
        var monthBills = await db.UtilityBills.AsNoTracking().Where(b => b.ContractId == contract.Id && b.Month == month && b.Year == year).ToListAsync();
        decimal totalBilled = await db.UtilityBills.Where(b => b.ContractId == contract.Id).SumAsync(b => (decimal?)b.TotalAmount) ?? 0;
        decimal totalPaid = await db.IncomeExpenses.Where(ie => ie.ContractId == contract.Id && ie.Category.Name == SystemCategories.RoomPayment)
            .SumAsync(ie => (decimal?)ie.Amount) ?? 0;
        decimal balance = totalPaid - totalBilled;
        var next = new DateTime(year, month, 1).AddMonths(1);

        var doc = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(40);
            page.MarginVertical(50);
            page.DefaultTextStyle(Base);

            page.Content().Column(col =>
            {
                col.Spacing(4);
                col.Item().AlignCenter().Text("HÓA ĐƠN THU TIỀN PHÒNG TRỌ").FontSize(18).Bold();
                col.Item().Text($"Số: {payment.ReferenceNumber}");
                col.Item().Text($"Ngày: {payment.TransactionDate:dd/MM/yyyy}");

                col.Item().PaddingTop(10).Text("I. THÔNG TIN HỢP ĐỒNG").FontSize(14).Bold();
                col.Item().Text($"Mã hợp đồng: #{contract.Id}");
                col.Item().Text($"Phòng: {string.Join(", ", contract.ContractRooms.Select(cr => cr.Room.Name))}");
                if (contract.IsCompany)
                {
                    col.Item().Text($"Công ty: {contract.Company?.CompanyName ?? "N/A"}");
                    col.Item().Text($"Mã số thuế: {contract.Company?.TaxCode ?? "N/A"}");
                }
                else
                {
                    var tenant = contract.ContractTenants.OrderBy(ct => ct.CreatedAt).FirstOrDefault()?.Tenant;
                    col.Item().Text($"Khách thuê: {tenant?.FullName ?? "N/A"}");
                    col.Item().Text($"CMND/CCCD: {tenant?.IdentityCard ?? "N/A"}");
                    col.Item().Text($"Điện thoại: {tenant?.PhoneNumber ?? "N/A"}");
                }

                if (monthBills.Count > 0)
                {
                    col.Item().PaddingTop(10).Text($"II. PHIẾU BÁO THÁNG {month}/{year}").FontSize(14).Bold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(6); c.RelativeColumn(4); });
                        void Row(string label, decimal value, bool bold = false)
                        {
                            var l = t.Cell().PaddingBottom(4).Text(label);
                            var v = t.Cell().PaddingBottom(4).AlignRight().Text(Fmt.Vnd(value));
                            if (bold) { l.Bold(); v.Bold(); }
                        }
                        Row("Tiền phòng:", monthBills.Sum(b => b.RentAmount));
                        Row("Tiền điện:", monthBills.Sum(b => b.ElectricityAmount));
                        Row("Tiền nước:", monthBills.Sum(b => b.WaterAmount));
                        if (monthBills.Sum(b => b.ExtraCharge) > 0) Row("Phụ thu:", monthBills.Sum(b => b.ExtraCharge));
                        if (monthBills.Sum(b => b.Discount) > 0) Row("Giảm giá:", monthBills.Sum(b => b.Discount));
                        Row("TỔNG CỘNG:", monthBills.Sum(b => b.TotalAmount), true);
                    });
                }

                col.Item().PaddingTop(10).Text("III. THÔNG TIN THANH TOÁN").FontSize(14).Bold();
                col.Item().Text($"Số tiền thu: {Fmt.Vnd(payment.Amount)}").Bold();
                col.Item().Text($"Bằng chữ: {VietnameseNumber.ToWords(payment.Amount)}");
                col.Item().Text($"Ghi chú: {payment.Description}");

                col.Item().PaddingTop(10).Text("IV. TIỀN CỌC").FontSize(14).Bold();
                col.Item().Text(balance >= 0 ? $"Tiền thừa (đặt cọc): {Fmt.Vnd(balance)}" : $"Âm cọc: {Fmt.Vnd(Math.Abs(balance))}").Bold();

                col.Item().PaddingTop(10).Text("V. LƯU Ý QUAN TRỌNG").FontSize(14).Bold();
                col.Item().Text($"• Hạn đóng tiền tháng tới: {L.PaymentDueDay:00}/{next.Month:00}/{next.Year}");
                col.Item().Text($"• Đề nghị chụp đồng hồ nước vào ngày {Math.Max(1, L.PaymentDueDay - 1):00}/{next.Month:00}/{next.Year} để tính phí nước");
                col.Item().Text("• Điện được tính theo công tơ điện tử");
                col.Item().Text("• Vui lòng giữ hóa đơn này để đối chiếu khi cần");

                col.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text("Người nộp tiền");
                    row.RelativeItem().AlignCenter().Text("Người thu tiền");
                });
            });

            page.Footer().AlignCenter().Text(FooterText()).FontSize(10).FontColor(Colors.Grey.Darken1);
        }));

        return (doc.GeneratePdf(), $"HoaDon_{payment.ReferenceNumber}.pdf");
    }

    public byte[] TenantList(List<TenantRow> data, string? statusFilter)
    {
        var doc = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(25);
            page.MarginVertical(30);
            page.DefaultTextStyle(Base.FontSize(9));

            page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text("DANH SÁCH KHÁCH THUÊ").FontSize(18).Bold();
                col.Item().AlignRight().Text($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(10);
                if (!string.IsNullOrEmpty(statusFilter))
                    col.Item().AlignRight().Text($"Trạng thái: {TenantStatusText(statusFilter)}").FontSize(10);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().Table(table =>
                {
                    float[] widths = [3, 12, 10, 10, 8, 5, 8, 8, 12, 8, 8, 5];
                    table.ColumnsDefinition(c => { foreach (var w in widths) c.RelativeColumn(w); });
                    table.Header(h =>
                    {
                        foreach (var t in new[] { "STT", "Họ tên", "CCCD", "Điện thoại", "Ngày sinh", "Giới tính", "Phòng", "Loại HĐ", "Công ty", "Trạng thái", "Ngày vào", "Chính" })
                            h.Cell().Border(0.5f).Background(Colors.Grey.Lighten2).Padding(4).AlignCenter().AlignMiddle().Text(t).Bold().FontSize(10);
                    });
                    int stt = 1;
                    foreach (var r in data)
                    {
                        void C(string? text, bool center = false)
                        {
                            var cell = table.Cell().Border(0.5f).Padding(3).AlignMiddle();
                            (center ? cell.AlignCenter() : cell).Text(text ?? "");
                        }
                        C((stt++).ToString(), true);
                        C(r.TenantName);
                        C(r.IdentityCard);
                        C(r.PhoneNumber);
                        C(Fmt.Date(r.BirthDate), true);
                        C(r.Gender, true);
                        C(r.RoomName, true);
                        C(ContractType.Display(r.ContractType), true);
                        C(r.CompanyName ?? "-");
                        C(TenantStatusText(r.ContractStatus), true);
                        C(Fmt.Date(r.MoveInDate), true);
                        C(r.IsPrimary ? "✓" : "", true);
                    }
                });

                col.Item().PaddingTop(12).Text("THỐNG KÊ:").Bold().FontSize(10);
                col.Item().Text($"- Tổng số người: {data.Count}").FontSize(10);
                col.Item().Text($"- Số phòng: {data.Select(x => x.RoomId).Distinct().Count()}").FontSize(10);
                if (data.Any(x => x.ContractType == ContractType.Company))
                    col.Item().Text($"- Số công ty: {data.Where(x => x.CompanyId.HasValue).Select(x => x.CompanyId).Distinct().Count()}").FontSize(10);
            });

            page.Footer().AlignCenter().Text(t => { t.CurrentPageNumber(); t.Span("/"); t.TotalPages(); });
        }));
        return doc.GeneratePdf();
    }

    public static string TenantStatusText(string? status) => status switch
    {
        ContractStatus.Active => "Đang ở",
        ContractStatus.Expired => "Hết hạn",
        _ => "Đã kết thúc"
    };

    private string FooterText()
    {
        var parts = new List<string> { L.Name };
        if (!string.IsNullOrWhiteSpace(L.Address)) parts.Add("Địa chỉ: " + L.Address);
        if (!string.IsNullOrWhiteSpace(L.Phone)) parts.Add("SĐT: " + L.Phone);
        return string.Join(" - ", parts);
    }
}
