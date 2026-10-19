using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>
/// Đưa phần mềm vào dùng thật (chạy: NHNhaTro.exe --import-tenants "danh sách tiền phòng trọ.xlsx"):
/// xóa toàn bộ dữ liệu thử, đặt cấu hình dãy/đồ đạc theo thực tế, nhập khách đang ở từ file Excel
/// (cột PHÒNG, NGƯỜI THUÊ (mỗi người một dòng trong ô), TIỀN PHÒNG, GHI CHÚ).
/// Khách đang ở được ghi ngày vào = ngày chốt kỳ hiện tại (tháng này coi như đã thu ngoài phần mềm), cọc mặc định đang giữ,
/// số CCCD tạm "CHUA-CO-..." chờ bổ sung, và đánh dấu "khách cũ" (không áp dụng quy định ở đủ số tháng mới trả cọc).
/// </summary>
public static partial class TenantImporter
{
    private const string User = "nhap-ban-dau";

    // Cấu hình theo chủ nhà (19/10/2026)
    private static readonly string[] Items = ["Điều hòa", "Bàn ghế", "Đệm", "Máy giặt", "Chăn ga", "Quạt", "Tủ lạnh"];
    private static readonly Dictionary<string, (decimal Rent, string[] Items)> BlockConfig = new()
    {
        ["A"] = (2_000_000, ["Điều hòa", "Bàn ghế", "Đệm"]),
        ["B"] = (2_200_000, ["Điều hòa", "Bàn ghế", "Máy giặt", "Đệm", "Chăn ga", "Quạt", "Tủ lạnh"]),
    };

    // Giá trị của dữ liệu nghiệm thu (DemoDataSeeder) cần dọn nếu còn
    private const string DemoAdTitle = "Phòng trọ mới, đủ đồ - An Phong, Hải Phòng";
    private static readonly string[] DemoGallery = ["phong-mau-1.png", "phong-mau-2.png", "mat-tien.png"];
    private static readonly string[] DemoAccountNumbers = ["0011000123456", "12510000999888"];

    public record Row(string RoomCode, List<string> Names, decimal Rent, string? Note);

    [GeneratedRegex(@"^([AB])(\d)\.?(\d)$")]
    private static partial Regex RoomCodeRx();

    /// <summary>"A 03", "A0.3", "B 1.2" → "A0.3", "B1.2".</summary>
    public static string? NormalizeRoomCode(string? raw)
    {
        var s = Regex.Replace(raw ?? "", @"\s+", "").ToUpperInvariant();
        var m = RoomCodeRx().Match(s);
        return m.Success ? $"{m.Groups[1].Value}{m.Groups[2].Value}.{m.Groups[3].Value}" : null;
    }

    /// <summary>"LÒ VĂN KHẢI" → "Lò Văn Khải".</summary>
    public static string ProperName(string raw)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var words = Regex.Split(raw.Trim().ToLower(vi), @"\s+").Where(w => w.Length > 0);
        return string.Join(" ", words.Select(w => char.ToUpper(w[0], vi) + w[1..]));
    }

    public static List<Row> ReadExcel(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        int header = 0, cRoom = 0, cName = 0, cRent = 0, cNote = 0;
        foreach (var row in ws.RowsUsed())
        {
            foreach (var cell in row.CellsUsed())
            {
                var t = cell.GetString().Trim().ToUpperInvariant();
                if (t == "PHÒNG") cRoom = cell.Address.ColumnNumber;
                else if (t.StartsWith("NGƯỜI THUÊ")) cName = cell.Address.ColumnNumber;
                else if (t.StartsWith("TIỀN PHÒNG")) cRent = cell.Address.ColumnNumber;
                else if (t.StartsWith("GHI CHÚ")) cNote = cell.Address.ColumnNumber;
            }
            if (cRoom > 0 && cRent > 0) { header = row.RowNumber(); break; }
        }
        if (header == 0) throw new InvalidOperationException("Không tìm thấy dòng tiêu đề có cột PHÒNG và TIỀN PHÒNG.");

        var list = new List<Row>();
        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > header))
        {
            var rawCode = row.Cell(cRoom).GetString();
            if (string.IsNullOrWhiteSpace(rawCode)) continue;
            var code = NormalizeRoomCode(rawCode) ?? throw new InvalidOperationException($"Dòng {row.RowNumber()}: mã phòng \"{rawCode}\" không đọc được.");
            var names = cName > 0
                ? row.Cell(cName).GetString().Split(['\n', '\r', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ProperName).ToList()
                : [];
            var rentCell = row.Cell(cRent);
            decimal rent = rentCell.TryGetValue<decimal>(out var d) ? d : MoneyInputParse(rentCell.GetString());
            var note = cNote > 0 ? row.Cell(cNote).GetString().Trim() : null;
            list.Add(new Row(code, names, rent, string.IsNullOrWhiteSpace(note) ? null : note));
        }
        var dup = list.GroupBy(r => r.RoomCode).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidOperationException($"Phòng {dup.Key} có 2 dòng.");
        return list;
    }

    private static decimal MoneyInputParse(string s)
    {
        var digits = new string(s.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? 0 : decimal.Parse(digits);
    }

    public static async Task<string> RunAsync(IServiceProvider sp, string excelPath)
    {
        if (!File.Exists(excelPath)) throw new FileNotFoundException("Không thấy file Excel: " + excelPath);
        var rows = ReadExcel(excelPath);
        if (rows.Count == 0) throw new InvalidOperationException("File không có dòng phòng nào.");
        var log = new List<string>();

        // 1) Xóa dữ liệu thử (khách, hợp đồng, phiếu, thu chi, sửa chữa, mua sắm, file)
        log.Add(await DemoDataSeeder.ClearAsync(sp));

        var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var files = sp.GetRequiredService<FileStorage>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.FirstAsync();
        var today = DateTime.Today;
        var (bm, by) = RoomService.CurrentBillingPeriod(s.BillingDay, today);
        var start = RoomService.BillingDate(bm, by, s.BillingDay);

        // 2) Dọn phần còn lại của dữ liệu nghiệm thu: tài khoản, quảng cáo, ảnh mẫu
        foreach (var a in await db.Accounts.ToListAsync())
        {
            if (a.AccountNumber != null && DemoAccountNumbers.Contains(a.AccountNumber)) { a.BankName = null; a.AccountNumber = null; a.AccountHolder = null; }
            if (a.OpeningBalance is 50_000_000 or 20_000_000) { a.OpeningBalance = 0; a.OpeningDate = start; }
        }
        if (s.AdTitle == DemoAdTitle || (s.AdDescription?.StartsWith("64 phòng khép kín") ?? false) || (s.AdDescription?.Contains("Phòng có tủ, giường") ?? false))
        {
            s.AdTitle = "Nhà trọ An Cư - phòng sạch, đầy đủ đồ, giá hợp lý";
            s.AdDescription = "64 phòng tại Thôn Đình Ngọ, An Phong, Hải Phòng. Dãy A có điều hòa, bàn ghế, đệm. Dãy B có điều hòa, bàn ghế, máy giặt, đệm, chăn ga, quạt, tủ lạnh. Điện 3.500đ/số, nước 80.000đ/người/tháng.";
        }
        var demoPics = await db.Attachments.Where(x => x.EntityType == AttachmentOwner.Site && x.FileName != null && DemoGallery.Contains(x.FileName)).ToListAsync();
        db.Attachments.RemoveRange(demoPics);
        await db.SaveChangesAsync();
        foreach (var p in demoPics) files.Delete(p.Path);
        if (demoPics.Count > 0) log.Add($"Đã xóa {demoPics.Count} ảnh quảng cáo mẫu.");

        // 3) Danh mục đồ đạc: giữ đúng danh sách, xóa món khác
        var allItems = await db.FurnitureItems.ToListAsync();
        for (int i = 0; i < Items.Length; i++)
        {
            var it = allItems.FirstOrDefault(x => string.Equals(x.Name, Items[i], StringComparison.OrdinalIgnoreCase));
            if (it == null) { it = new FurnitureItem { Name = Items[i] }; db.FurnitureItems.Add(it); allItems.Add(it); }
            it.Name = Items[i];
            it.SortOrder = i + 1;
            it.IsActive = true;
        }
        await db.SaveChangesAsync();
        var drop = allItems.Where(x => !Items.Contains(x.Name)).ToList();
        var dropIds = drop.Select(x => x.Id).ToList();
        await db.BlockFurniture.Where(x => dropIds.Contains(x.FurnitureItemId)).ExecuteDeleteAsync();
        await db.RoomFurniture.Where(x => dropIds.Contains(x.FurnitureItemId)).ExecuteDeleteAsync();
        await db.Repairs.Where(x => x.FurnitureItemId != null && dropIds.Contains(x.FurnitureItemId.Value)).ExecuteUpdateAsync(x => x.SetProperty(r => r.FurnitureItemId, (int?)null));
        db.FurnitureItems.RemoveRange(drop);
        await db.SaveChangesAsync();
        if (drop.Count > 0) log.Add("Đã xóa đồ đạc: " + string.Join(", ", drop.Select(x => x.Name)) + ".");

        // 4) Mặc định từng dãy, áp cho mọi phòng (sau bước 1 mọi phòng đều trống)
        var ids = await db.FurnitureItems.ToDictionaryAsync(x => x.Name, x => x.Id);
        var blocks = await db.Blocks.Include(b => b.DefaultFurniture).ToListAsync();
        var rooms = await db.Rooms.Include(r => r.Furniture).ToListAsync();
        foreach (var b in blocks)
        {
            if (!BlockConfig.TryGetValue(b.Code, out var cfg)) continue;
            var target = cfg.Items.ToDictionary(n => ids[n], _ => 1);
            b.DefaultRent = cfg.Rent;
            SetupService.SyncFurniture(b.DefaultFurniture, target, (id, q) => new BlockFurniture { BlockId = b.Id, FurnitureItemId = id, Quantity = q }, x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);
            foreach (var r in rooms.Where(r => r.BlockId == b.Id))
            {
                r.Rent = cfg.Rent;
                SetupService.SyncFurniture(r.Furniture, target, (id, q) => new RoomFurniture { RoomId = r.Id, FurnitureItemId = id, Quantity = q }, x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);
            }
            log.Add($"Dãy {b.Code}: giá mặc định {Fmt.Vnd(cfg.Rent)}, đồ đạc {string.Join(", ", cfg.Items)}.");
        }
        await db.SaveChangesAsync();

        // 5) Khách đang ở
        int people = 0, leases = 0;
        var noName = new List<string>();
        foreach (var row in rows.OrderBy(r => r.RoomCode))
        {
            var room = rooms.FirstOrDefault(r => r.Code == row.RoomCode) ?? throw new InvalidOperationException($"Không có phòng {row.RoomCode} trong phần mềm.");
            bool foreign = row.Note?.Contains("TRUNG QUỐC", StringComparison.OrdinalIgnoreCase) ?? false;
            var names = row.Names.Count > 0 ? row.Names : [foreign ? "Khách Trung Quốc (chưa có tên)" : "Chưa có tên"];
            if (row.Names.Count == 0) noName.Add(row.RoomCode);

            var lease = new Lease
            {
                RoomId = room.Id, StartDate = start, MonthlyRent = row.Rent > 0 ? row.Rent : room.Rent, Deposit = s.DefaultDeposit,
                Status = LeaseStatus.Active, IsExistingTenant = true, CreatedAt = DateTime.Now, CreatedBy = User,
                Note = $"Khách đang ở trước khi dùng phần mềm, nhập từ danh sách ngày {today:dd/MM/yyyy}" + (row.Note != null ? $" ({row.Note})" : "")
                       + ". Cần bổ sung số CCCD, điện thoại, ảnh."
            };
            for (int i = 0; i < names.Count; i++)
            {
                var tenant = new Tenant
                {
                    FullName = names[i], IdentityCard = $"CHUA-CO-{row.RoomCode}-{i + 1}",
                    Nationality = foreign ? "Trung Quốc" : "Việt Nam", Ethnicity = foreign ? null : "Kinh"
                };
                lease.Tenants.Add(new LeaseTenant { Tenant = tenant, IsRepresentative = i == 0, MoveInDate = start });
                people++;
            }
            db.Leases.Add(lease);
            room.Status = RoomStatus.Occupied;
            room.Rent = lease.MonthlyRent;
            await db.SaveChangesAsync();

            if (s.DefaultDeposit > 0)
                db.Transactions.Add(new MoneyTransaction
                {
                    AccountId = s.PersonalAccountId, Date = start, Amount = s.DefaultDeposit, Category = TxCategory.Deposit,
                    Description = $"Cọc phòng {room.Code} (khách đang ở, nhập ban đầu)", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = User
                });
            leases++;
        }
        await db.SaveChangesAsync();

        var empty = rooms.Where(r => r.Status != RoomStatus.Occupied).Select(r => r.Code).OrderBy(c => c).ToList();
        log.Add($"Đã nhập {leases} phòng có khách, {people} người, tổng tiền phòng {Fmt.Vnd(rows.Sum(r => r.Rent))}/tháng.");
        log.Add($"Ngày vào ghi {start:dd/MM/yyyy}: kỳ tháng {bm:00}/{by} coi như đã thu, kỳ thu đầu trên phần mềm là {start.AddMonths(1):dd/MM/yyyy} (đủ tháng).");
        log.Add($"Cọc {Fmt.Vnd(s.DefaultDeposit)}/phòng ghi đang giữ ở TK cá nhân.");
        if (noName.Count > 0) log.Add("Phòng chưa có tên khách: " + string.Join(", ", noName) + ".");
        log.Add("Phòng trống: " + (empty.Count > 0 ? string.Join(", ", empty) : "không có") + ".");
        return string.Join(Environment.NewLine, log);
    }
}
