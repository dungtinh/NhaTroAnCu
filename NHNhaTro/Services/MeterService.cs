using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Kết quả nhập một ảnh công tơ: đọc được gì, xếp vào phòng/kỳ nào, lỗi nếu có.</summary>
public class MeterImportItem
{
    public required PendingFile File { get; init; }
    public string? RoomCode { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
    public decimal? Kwh { get; set; }
    public decimal? OldReading { get; set; }
    public decimal? NewReading { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? LeaseId { get; set; }
    public int? ReadingId { get; set; }
    /// <summary>Lỗi không nhập được (ảnh không đọc được, phòng không tồn tại...).</summary>
    public string? Error { get; set; }
    /// <summary>Nhập được nhưng cần lưu ý (phòng trống, kỳ đã lập phiếu...).</summary>
    public string? Warning { get; set; }
    public bool Done { get; set; }
    public bool Ok => Done && Error == null;
    public string Period => Month is int m && Year is int y ? $"{m:00}/{y}" : "?";
}

/// <summary>Một phiếu trên link của khách kèm số đã đóng và các lần đóng.</summary>
public record PortalBill(Bill Bill, decimal Paid, string RentDetail, List<MoneyTransaction> Payments)
{
    public decimal Due => Math.Max(0, Bill.TotalAmount - Paid);
}

/// <summary>Kỳ sắp phải đóng (chưa lập phiếu): tiền phòng, nước, sạc xe theo cấu hình; điện tạm tính theo ảnh công tơ nếu đã có.</summary>
public record PortalUpcoming(int Month, int Year, DateTime Due, decimal Rent, string RentDetail, int Persons, decimal Water, decimal EvCharge, MeterReading? Reading, decimal? Electricity)
{
    public decimal Total => Rent + Water + EvCharge + (Electricity ?? 0);
}

/// <summary>Dữ liệu trang khách tự kiểm tra (/khach/{token}).</summary>
public class TenantPortal
{
    public required AppSetting Settings { get; init; }
    public required Lease Lease { get; init; }
    public required Room Room { get; init; }
    public string? Representative { get; init; }
    public int Persons { get; init; }
    public List<PortalBill> Bills { get; init; } = [];
    public List<MeterReading> Readings { get; init; } = [];
    public PortalUpcoming? Upcoming { get; init; }
    public MoneyAccount? BankAccount { get; init; }
}

/// <summary>
/// Ảnh công tơ điện tử: nhập hàng loạt ảnh chụp màn hình, AI đọc mã phòng, khoảng ngày và số điện rồi xếp vào phòng, kỳ thu;
/// link cho khách tự xem ảnh và phiếu.
/// </summary>
public class MeterService(IDbContextFactory<ApplicationDbContext> dbFactory, DocumentScanService scanner, FileStorage files)
{
    public const string UploadKind = "Meter";

    /// <summary>
    /// Kỳ thu (tháng/năm phiếu) của một khoảng ngày trên ảnh: lấy ngày giữa khoảng, phiếu là kỳ chốt (mồng 10) đầu tiên sau ngày đó.
    /// Ảnh 10/10 → 10/11 (hoặc 10/10 → 31/10) thuộc phiếu 11; ảnh 20/09 → 10/10 (kỳ chốt đầu) thuộc phiếu 10.
    /// </summary>
    public static (int Month, int Year) PeriodFor(DateTime? from, DateTime? to, int billingDay, (int Month, int Year) fallback)
    {
        DateTime mid;
        if (from is DateTime f && to is DateTime t && t >= f) mid = f.AddDays((t - f).TotalDays / 2);
        else if (to is DateTime t2) mid = t2.AddDays(-1);
        else if (from is DateTime f2) mid = f2;
        else return fallback;
        var due = RoomService.BillingDate(mid.Month, mid.Year, billingDay);
        if (due > mid.Date) return (mid.Month, mid.Year);
        var n = mid.AddMonths(1);
        return (n.Month, n.Year);
    }

    public async Task<List<MeterReading>> ListAsync(int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.MeterReadings.AsNoTracking().Where(r => r.Month == month && r.Year == year).ToListAsync();
    }

    /// <summary>Nhập một loạt ảnh, tối đa 3 ảnh đọc song song; gọi <paramref name="progress"/> mỗi khi xong một ảnh.</summary>
    public async Task<List<MeterImportItem>> ImportAsync(IReadOnlyList<PendingFile> uploads, (int Month, int Year) fallback, string? user, Func<MeterImportItem, Task>? progress = null)
    {
        var items = uploads.Select(f => new MeterImportItem { File = f }).ToList();
        using var gate = new SemaphoreSlim(3);
        await Task.WhenAll(items.Select(async item =>
        {
            await gate.WaitAsync();
            try { await ImportOneAsync(item, fallback, user); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { item.Error = "Lỗi khi nhập ảnh: " + ex.Message; }
            finally { gate.Release(); }
            item.Done = true;
            if (progress != null) await progress(item);
        }));
        return items;
    }

    private async Task ImportOneAsync(MeterImportItem item, (int Month, int Year) fallback, string? user)
    {
        MeterScanResult scan;
        try { scan = await scanner.ScanMeterAsync(item.File.Path); }
        catch (InvalidOperationException ex) { item.Error = ex.Message; return; }
        catch (HttpRequestException ex) { item.Error = "Không kết nối được dịch vụ đọc ảnh: " + ex.Message; return; }
        catch (TaskCanceledException) { item.Error = "Đọc ảnh quá lâu (dịch vụ AI chậm), bấm \"Đọc lại ảnh lỗi\"."; return; }

        item.RoomCode = scan.RoomCode;
        item.Kwh = scan.Kwh;
        item.OldReading = scan.OldReading;
        item.NewReading = scan.NewReading;
        item.From = scan.From;
        item.To = scan.To;
        if (item.Kwh == null && item.OldReading is decimal o && item.NewReading is decimal n && n >= o) item.Kwh = n - o;
        if (string.IsNullOrWhiteSpace(item.RoomCode)) { item.Error = "Không đọc được mã phòng trên ảnh."; return; }
        if (item.Kwh is not decimal kwh || kwh < 0) { item.Error = $"Phòng {item.RoomCode}: không đọc được số điện trên ảnh."; return; }

        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var code = NormalizeCode(item.RoomCode);
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code);
        if (room == null) { item.Error = $"Không có phòng \"{item.RoomCode}\" trong phần mềm."; return; }
        item.RoomCode = room.Code;
        var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.RoomId == room.Id && l.Status == LeaseStatus.Active);
        item.LeaseId = lease?.Id;
        if (lease == null) item.Warning = $"Phòng {room.Code} đang trống, chỉ lưu ảnh.";

        (item.Month, item.Year) = PeriodFor(scan.From, scan.To, s.BillingDay, fallback);
        if (scan.From == null && scan.To == null) item.Warning = $"Không đọc được khoảng ngày, xếp vào kỳ {item.Period} đang mở.";
        if (lease != null && await db.Bills.AnyAsync(b => b.LeaseId == lease.Id && b.Kind == BillKind.Monthly && b.Month == item.Month && b.Year == item.Year))
            item.Warning = $"Kỳ {item.Period} của phòng {room.Code} đã lập phiếu: chỉ lưu ảnh, không đổi số điện trên phiếu.";

        var existing = await db.MeterReadings.FirstOrDefaultAsync(r => r.RoomId == room.Id && r.Month == item.Month && r.Year == item.Year);
        string? oldPath = null;
        if (existing == null)
        {
            existing = new MeterReading { RoomId = room.Id, Month = item.Month!.Value, Year = item.Year!.Value };
            db.MeterReadings.Add(existing);
        }
        else oldPath = existing.ImagePath;
        existing.LeaseId = lease?.Id;
        existing.Kwh = Math.Round(kwh, 2);
        existing.OldReading = scan.OldReading;
        existing.NewReading = scan.NewReading;
        existing.PeriodFrom = scan.From;
        existing.PeriodTo = scan.To;
        existing.ImagePath = item.File.Path;
        existing.CreatedAt = DateTime.Now;
        existing.CreatedBy = user;
        await db.SaveChangesAsync();
        item.ReadingId = existing.Id;
        if (oldPath != null && oldPath != item.File.Path) files.Delete(oldPath);
    }

    /// <summary>"b1.8", "B 1.8", "B1-8" → "B1.8".</summary>
    private static string NormalizeCode(string raw)
    {
        var s = raw.Trim().ToUpperInvariant().Replace(" ", "").Replace('-', '.').Replace(',', '.').Replace('_', '.');
        // "B18" (thiếu dấu chấm) → "B1.8"
        if (s.Length == 3 && char.IsLetter(s[0]) && char.IsDigit(s[1]) && char.IsDigit(s[2])) s = $"{s[0]}{s[1]}.{s[2]}";
        return s;
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var r = await db.MeterReadings.FindAsync(id);
        if (r == null) return;
        db.MeterReadings.Remove(r);
        await db.SaveChangesAsync();
        files.Delete(r.ImagePath);
    }

    // ---------------- Link cho khách ----------------

    /// <summary>Mã link của lượt thuê, tạo nếu chưa có.</summary>
    public async Task<string> GetOrCreateTokenAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.FirstAsync(l => l.Id == leaseId);
        if (string.IsNullOrEmpty(lease.PublicToken))
        {
            lease.PublicToken = NewToken();
            await db.SaveChangesAsync();
        }
        return lease.PublicToken;
    }

    /// <summary>Đổi mã link (link cũ hết hiệu lực).</summary>
    public async Task<string> ResetTokenAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.FirstAsync(l => l.Id == leaseId);
        lease.PublicToken = NewToken();
        await db.SaveChangesAsync();
        return lease.PublicToken;
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

    /// <summary>Đường dẫn vật lý ảnh công tơ theo mã link của khách: chỉ ảnh thuộc lượt thuê đó.</summary>
    public async Task<string?> ResolveImageAsync(string token, int readingId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 40) return null;
        await using var db = await dbFactory.CreateDbContextAsync();
        var path = await db.MeterReadings.AsNoTracking().Where(r => r.Id == readingId && r.Lease != null && r.Lease.PublicToken == token)
            .Select(r => r.ImagePath).FirstOrDefaultAsync();
        return path == null ? null : files.ResolvePhysicalPath(path);
    }

    public async Task<TenantPortal?> GetPortalAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 40) return null;
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.AsNoTracking().Include(l => l.Room).ThenInclude(r => r.Block).Include(l => l.Tenants).ThenInclude(t => t.Tenant)
            .AsSplitQuery().FirstOrDefaultAsync(l => l.PublicToken == token);
        if (lease == null) return null;
        if (lease.CompanyContractId != null)
        {
            await CompanyService.EnsureBillsAsync(db, DateTime.Today);
            lease.CompanyContract = await db.CompanyContracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == lease.CompanyContractId);
        }
        var s = await db.Settings.AsNoTracking().FirstAsync();

        var bills = await db.Bills.AsNoTracking().Where(b => b.LeaseId == lease.Id).Include(b => b.Payments.OrderBy(p => p.Date))
            .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month).ThenByDescending(b => b.Id).ToListAsync();
        var portalBills = bills.Select(b => new PortalBill(b, b.Payments.Sum(p => p.Amount), LeaseService.RentDetail(b, lease, s.BillingDay), b.Payments)).ToList();
        var readings = await db.MeterReadings.AsNoTracking().Where(r => r.LeaseId == lease.Id).OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ToListAsync();

        var active = lease.Tenants.Where(t => t.MoveOutDate == null).ToList();
        var rep = active.FirstOrDefault(t => t.IsRepresentative) ?? active.FirstOrDefault();

        // Kỳ sắp phải đóng: kỳ gần nhất đã/sắp đến ngày chốt mà chưa lập phiếu (từ kỳ hiện tại, tối đa 3 kỳ tới).
        PortalUpcoming? upcoming = null;
        if (lease.Status == LeaseStatus.Active)
        {
            var (cm, cy) = RoomService.CurrentBillingPeriod(s.BillingDay);
            var d = new DateTime(cy, cm, 1);
            for (int i = 0; i < 3 && upcoming == null; i++, d = d.AddMonths(1))
            {
                var due = RoomService.BillingDate(d.Month, d.Year, s.BillingDay);
                if (!RoomService.NeedsMonthlyBill(lease, d.Month, d.Year, s.BillingDay)) continue;
                if (lease.IsExistingTenant && due <= lease.CreatedAt.Date) continue;
                if (bills.Any(b => b.Kind == BillKind.Monthly && b.Month == d.Month && b.Year == d.Year)) continue;
                bool first = lease.CompanyContract == null && RoomService.IsFirstBillingMonth(lease.StartDate, d.Month, d.Year, s.BillingDay);
                decimal rent; string detail;
                if (lease.CompanyContract != null) { rent = 0; detail = "Tiền phòng thu theo kỳ hợp đồng công ty"; }
                else if (first)
                {
                    var stub = LeaseService.CalculateStubPeriod(lease.MonthlyRent, lease.StartDate, s.BillingDay);
                    rent = stub.Rent; detail = LeaseService.RentDetail(stub.From, stub.To, lease.StartDate, lease.MonthlyRent, s.BillingDay);
                }
                else { rent = lease.MonthlyRent; detail = LeaseService.RentDetail(due, due.AddMonths(1), lease.StartDate, lease.MonthlyRent, s.BillingDay); }
                var water = BillingService.CompanyWater(lease) ?? active.Count * s.WaterPricePerPerson;
                var reading = readings.FirstOrDefault(r => r.Month == d.Month && r.Year == d.Year);
                upcoming = new PortalUpcoming(d.Month, d.Year, due, rent, detail, active.Count, water, lease.EvCharging ? s.EvChargingFee : 0,
                    reading, reading == null ? null : reading.KwhRounded * s.ElectricityPrice);
            }
        }

        var bank = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == s.BusinessAccountId);
        return new TenantPortal
        {
            Settings = s, Lease = lease, Room = lease.Room, Representative = rep?.Tenant.FullName, Persons = active.Count,
            Bills = portalBills, Readings = readings, Upcoming = upcoming,
            BankAccount = bank is { AccountNumber.Length: > 0 } ? bank : null
        };
    }
}
