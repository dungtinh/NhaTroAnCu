using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class TenantInput
{
    public int? TenantId { get; set; }
    public string FullName { get; set; } = "";
    public string IdentityCard { get; set; } = "";
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? PermanentAddress { get; set; }
    public string? Ethnicity { get; set; } = "Kinh";
    public string? Nationality { get; set; } = "Việt Nam";
    public string? VehiclePlate { get; set; }
    public List<string> Photos { get; set; } = [];
    /// <summary>Ảnh chân dung.</summary>
    public string? Portrait { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(FullName) && string.IsNullOrWhiteSpace(IdentityCard);

    public static TenantInput From(Tenant t) => new()
    {
        TenantId = t.Id, FullName = t.FullName, IdentityCard = t.IdentityCard, BirthDate = t.BirthDate, Gender = t.Gender, Phone = t.Phone,
        PermanentAddress = t.PermanentAddress, Ethnicity = t.Ethnicity, Nationality = t.Nationality, VehiclePlate = t.VehiclePlate, Photos = t.PhotoList.ToList(), Portrait = t.Portrait
    };
}

public class CheckInForm
{
    public int RoomId { get; set; }
    public DateTime MoveInDate { get; set; } = DateTime.Today;
    public decimal MonthlyRent { get; set; }
    public decimal Deposit { get; set; }
    public string? Note { get; set; }
    public List<TenantInput> Tenants { get; set; } = [new()];

    // Thu tiền ngay khi nhận phòng
    public decimal DepositPaid { get; set; }
    public decimal FirstPaymentPaid { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public List<PendingFile> DepositReceipts { get; set; } = [];
    public List<PendingFile> PaymentReceipts { get; set; } = [];
}

/// <summary>
/// Đoạn lẻ đầu: tiền phòng từ ngày vào đến ngày chốt đầu (mồng 10 tháng sau tháng nhận phòng), thu ở kỳ chốt đầu.
/// Tháng trả trước lúc nhận phòng bao kỳ tròn tháng kế tiếp [To, To + 1 tháng).
/// </summary>
public record StubPeriod(DateTime From, DateTime To, decimal Rent, int Days);

public class PenaltyInput
{
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
}

public class CheckoutForm
{
    public int LeaseId { get; set; }
    public DateTime EndDate { get; set; } = DateTime.Today;
    /// <summary>Số điện đã dùng từ kỳ chốt trước đến ngày trả (kWh).</summary>
    public int Kwh { get; set; }
    /// <summary>Số ngày khách xin ở lại thêm để thu xếp: tính tiền phòng theo ngày (giá tháng ÷ 30).</summary>
    public int ExtraDays { get; set; }
    public List<PenaltyInput> Penalties { get; set; } = [];
    /// <summary>Khoản khác: dương = khách phải trả thêm, âm = hoàn lại khách.</summary>
    public decimal OtherAmount { get; set; }
    public string? OtherNote { get; set; }
    public string? Note { get; set; }
    /// <summary>Số lượng đồ đạc thực tế khi kiểm.</summary>
    public Dictionary<int, int> FurnitureActual { get; set; } = [];
    public bool UpdateRoomFurniture { get; set; }
    public decimal DepositRefund { get; set; }
    public decimal Collected { get; set; }
    public List<PendingFile> Photos { get; set; } = [];
    public List<PendingFile> RefundReceipts { get; set; } = [];
}

/// <summary>Kỳ đã đến ngày thu mà chưa lập phiếu: tính đủ tháng tiền phòng và nước vào phiếu trả phòng.</summary>
public record MissingPeriod(int Month, int Year, DateTime From, DateTime To, decimal Rent, decimal Water, int PersonCount);

public record CheckoutPreview(decimal Deposit, int UsedKwh, decimal Electricity, decimal Penalties, decimal Other,
    decimal UnpaidBills, List<(Bill Bill, decimal Due)> UnpaidList, List<string> FurnitureWarnings, List<MissingPeriod> Missing,
    bool DepositRefundable, int MinStayMonths, DateTime RefundEligibleFrom, int StayDays,
    int ExtraDays = 0, decimal ExtraDayRate = 0, decimal ExtraStay = 0)
{
    public decimal MissingRent => Missing.Sum(m => m.Rent);
    public decimal MissingWater => Missing.Sum(m => m.Water);
    /// <summary>Tổng các khoản khách phải trả khi ra: điện kỳ cuối, phạt, khoản khác, nợ cũ, tiền phòng/nước kỳ chưa lập.</summary>
    public decimal Deductions => Electricity + Penalties + Other + UnpaidBills + MissingRent + MissingWater + ExtraStay;
    /// <summary>Cọc được tính vào quyết toán: ở chưa đủ số tháng tối thiểu thì cọc không hoàn trả (0).</summary>
    public decimal RefundableDeposit => DepositRefundable ? Deposit : 0;
    /// <summary>Phần cọc không hoàn trả do ở chưa đủ tháng tối thiểu.</summary>
    public decimal Forfeited => Deposit - RefundableDeposit;
    /// <summary>Dương = trả lại khách, âm = khách phải nộp thêm.</summary>
    public decimal Balance => RefundableDeposit - Deductions;
}

public record CardScanResult(string? Id, string? Name, DateTime? BirthDate, string? Gender, string? Address, string? Ethnicity, string? Nationality);

public record ResidentRow(int LeaseId, string RoomCode, string FullName, string IdentityCard, DateTime? BirthDate, string? Gender, string? Phone,
    string? Ethnicity, string? Nationality, string? PermanentAddress, string? VehiclePlate, DateTime MoveInDate, DateTime? MoveOutDate, bool IsRepresentative, string? Photo);

public static class Nationality
{
    public static bool IsForeign(string? nationality, string? ethnicity)
    {
        if (!string.IsNullOrWhiteSpace(nationality))
        {
            var n = nationality.Trim().ToLower(Fmt.Vi);
            return !(n is "việt nam" or "viet nam" or "vn" or "vietnam");
        }
        if (string.IsNullOrWhiteSpace(ethnicity)) return false;
        var e = ethnicity.ToLower(Fmt.Vi);
        return new[] { "trung quốc", "china", "chinese", "nước ngoài", "foreign", "lào", "campuchia", "thái lan", "hàn quốc", "nhật" }.Any(e.Contains);
    }
}

public class LeaseService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public static decimal RoundMoney(decimal v) => Math.Round(v / 1000m, MidpointRounding.AwayFromZero) * 1000m;

    /// <summary>
    /// Đoạn lẻ đầu: từ ngày vào đến ngày chốt đầu, là mồng 10 gần nhất sau ngày vào (vào trước mồng 10 → mồng 10 ngay tháng đó;
    /// vào sau mồng 10 → mồng 10 tháng sau). Tính theo tháng 30 ngày, tiền = giá tháng × số ngày / 30, làm tròn nghìn đồng.
    /// Vào 01/09 → chốt 10/09, 9 ngày; vào 20/09 → chốt 10/10, 20 ngày; vào đúng mồng 10 → không có đoạn lẻ (0 ngày),
    /// tháng trả trước tính từ ngày vào, kỳ thu đầu là mồng 10 tháng sau với đủ tháng.
    /// </summary>
    public static StubPeriod CalculateStubPeriod(decimal rent, DateTime moveIn, int billingDay)
    {
        moveIn = moveIn.Date;
        var sameMonth = RoomService.BillingDate(moveIn.Month, moveIn.Year, billingDay);
        if (moveIn.Day <= billingDay)
        {
            int d = billingDay - moveIn.Day;
            return new StubPeriod(moveIn, moveIn.Day == billingDay ? moveIn : sameMonth, RoundMoney(rent * d / 30m), d);
        }
        var next = moveIn.AddMonths(1);
        int days = 30 + billingDay - moveIn.Day;
        return new StubPeriod(moveIn, RoomService.BillingDate(next.Month, next.Year, billingDay), RoundMoney(rent * days / 30m), days);
    }

    /// <summary>Phiếu này có phải phiếu kỳ chốt đầu (tiền phòng đoạn lẻ từ ngày vào) không: khoảng tính tiền đúng bằng đoạn lẻ.</summary>
    public static bool IsStubBill(DateTime periodFrom, DateTime periodTo, DateTime startDate, int billingDay)
    {
        var stub = CalculateStubPeriod(0, startDate, billingDay);
        return stub.Days > 0 && periodFrom.Date == stub.From && periodTo.Date == stub.To;
    }

    /// <summary>Diễn giải tiền phòng của một phiếu theo loại phiếu (trả trước khi vào, hằng tháng, trả phòng).</summary>
    public static string RentDetail(Bill bill, Lease lease, int billingDay) => bill.Kind switch
    {
        BillKind.MoveIn => $"1 tháng trả trước khi vào, tính cho {bill.PeriodFrom:dd/MM} → {bill.PeriodTo:dd/MM}",
        BillKind.MoveOut => $"Kỳ chưa lập phiếu, tính đủ tháng {bill.PeriodFrom:dd/MM} → {bill.PeriodTo:dd/MM}",
        _ => RentDetail(bill.PeriodFrom, bill.PeriodTo, lease.StartDate, lease.MonthlyRent, billingDay)
    };

    /// <summary>Diễn giải cách tính tiền phòng của một kỳ: "Kỳ đầu: 20 ngày (20/09 → 10/10) × 2.000.000 ÷ 30" hay "Tròn tháng 10/10 → 10/11".</summary>
    public static string RentDetail(DateTime periodFrom, DateTime periodTo, DateTime startDate, decimal monthlyRent, int billingDay)
    {
        if (IsStubBill(periodFrom, periodTo, startDate, billingDay))
        {
            var stub = CalculateStubPeriod(monthlyRent, startDate, billingDay);
            return $"Kỳ đầu: {stub.Days} ngày ({periodFrom:dd/MM} → {periodTo:dd/MM}) × {Fmt.Money(monthlyRent)} ÷ 30";
        }
        return $"Tròn tháng {periodFrom:dd/MM} → {periodTo:dd/MM}";
    }

    public async Task<TenantInput?> FindTenantAsync(string identityCard)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.IdentityCard == identityCard.Trim());
        return t == null ? null : TenantInput.From(t);
    }

    /// <summary>Người này đang ở phòng khác chưa trả phòng?</summary>
    public async Task<string?> FindActiveRoomOfAsync(string identityCard, int? exceptLeaseId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LeaseTenants.Where(lt => lt.Tenant.IdentityCard == identityCard.Trim() && lt.MoveOutDate == null
                && lt.Lease.Status == LeaseStatus.Active && lt.LeaseId != (exceptLeaseId ?? 0))
            .Select(lt => lt.Lease.Room.Code).FirstOrDefaultAsync();
    }

    private static void ValidateTenants(List<TenantInput> list)
    {
        if (list.Count == 0) throw new InvalidOperationException("Nhập ít nhất một người thuê.");
        foreach (var t in list)
        {
            if (string.IsNullOrWhiteSpace(t.FullName) || string.IsNullOrWhiteSpace(t.IdentityCard))
                throw new InvalidOperationException("Mỗi người thuê cần có họ tên và số CCCD/hộ chiếu.");
            t.FullName = t.FullName.Trim();
            t.IdentityCard = t.IdentityCard.Trim();
        }
        var dup = list.GroupBy(t => t.IdentityCard).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new InvalidOperationException($"Số CCCD {dup.Key} bị nhập trùng.");
    }

    private static async Task<Tenant> UpsertTenantAsync(ApplicationDbContext db, TenantInput input)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.IdentityCard == input.IdentityCard);
        if (tenant == null)
        {
            tenant = new Tenant();
            db.Tenants.Add(tenant);
        }
        tenant.FullName = input.FullName;
        tenant.IdentityCard = input.IdentityCard;
        tenant.BirthDate = input.BirthDate;
        tenant.Gender = input.Gender;
        tenant.Phone = input.Phone;
        tenant.PermanentAddress = input.PermanentAddress;
        tenant.Ethnicity = input.Ethnicity;
        tenant.Nationality = input.Nationality;
        tenant.VehiclePlate = input.VehiclePlate;
        tenant.Photos = input.Photos.Count > 0 ? string.Join(";", input.Photos) : null;
        tenant.Portrait = string.IsNullOrWhiteSpace(input.Portrait) ? null : input.Portrait;
        return tenant;
    }

    public async Task<int> CheckInAsync(CheckInForm form, string? user)
    {
        var tenants = form.Tenants.Where(t => !t.IsEmpty).ToList();
        ValidateTenants(tenants);
        if (form.MonthlyRent <= 0) throw new InvalidOperationException("Giá thuê phải lớn hơn 0.");
        if (form.Deposit < 0 || form.DepositPaid < 0 || form.FirstPaymentPaid < 0) throw new InvalidOperationException("Số tiền không được âm.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstAsync();
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == form.RoomId) ?? throw new InvalidOperationException("Không tìm thấy phòng.");
        if (room.Status == RoomStatus.Occupied || await db.Leases.AnyAsync(l => l.RoomId == room.Id && l.Status == LeaseStatus.Active))
            throw new InvalidOperationException($"Phòng {room.Code} đang có người thuê.");
        if (room.Status == RoomStatus.Maintenance)
            throw new InvalidOperationException($"Phòng {room.Code} đang sửa, chưa cho thuê được (bỏ đánh dấu ở trang phòng trước).");
        if (form.MoveInDate.Date > DateTime.Today.AddDays(31)) throw new InvalidOperationException("Ngày vào quá xa trong tương lai, kiểm tra lại.");
        if (form.MoveInDate.Date < DateTime.Today.AddYears(-2)) throw new InvalidOperationException("Ngày vào quá xa trong quá khứ, kiểm tra lại.");
        if (form.FirstPaymentPaid > form.MonthlyRent) throw new InvalidOperationException($"Tiền phòng đã thu ({Fmt.Vnd(form.FirstPaymentPaid)}) lớn hơn 1 tháng tiền phòng ({Fmt.Vnd(form.MonthlyRent)}).");
        if (form.DepositPaid > form.Deposit) throw new InvalidOperationException($"Tiền cọc đã thu ({Fmt.Vnd(form.DepositPaid)}) lớn hơn tiền cọc ({Fmt.Vnd(form.Deposit)}).");
        if (tenants.Count > room.MaxOccupants) throw new InvalidOperationException($"Phòng {room.Code} tối đa {room.MaxOccupants} người.");
        foreach (var t in tenants)
        {
            var other = await FindActiveRoomOfAsync(t.IdentityCard);
            if (other != null) throw new InvalidOperationException($"{t.FullName} đang ở phòng {other}, hãy trả phòng đó trước.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var lease = new Lease
        {
            RoomId = room.Id, StartDate = form.MoveInDate.Date, MonthlyRent = form.MonthlyRent, Deposit = form.Deposit,
            Note = form.Note, Status = LeaseStatus.Active, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Leases.Add(lease);
        bool first = true;
        foreach (var input in tenants)
        {
            var tenant = await UpsertTenantAsync(db, input);
            lease.Tenants.Add(new LeaseTenant { Tenant = tenant, IsRepresentative = first, MoveInDate = lease.StartDate });
            first = false;
        }

        // Thu đủ 1 tháng tiền phòng khi vào: tháng này tính cho kỳ tròn tháng đầu [chốt đầu, chốt đầu + 1 tháng).
        // Đoạn lẻ từ ngày vào đến ngày chốt đầu, điện và nước thu ở kỳ chốt đầu (Thu tiền tháng).
        var stub = CalculateStubPeriod(form.MonthlyRent, lease.StartDate, settings.BillingDay);
        var bill = new Bill
        {
            Lease = lease, Kind = BillKind.MoveIn, Month = lease.StartDate.Month, Year = lease.StartDate.Year,
            PeriodFrom = stub.To, PeriodTo = stub.To.AddMonths(1), RentAmount = form.MonthlyRent, PersonCount = tenants.Count, TotalAmount = form.MonthlyRent,
            Note = stub.Days > 0
                ? $"1 tháng tiền phòng trả trước khi nhận phòng {lease.StartDate:dd/MM/yyyy}, tính cho {stub.To:dd/MM} - {stub.To.AddMonths(1):dd/MM/yyyy}. Tiền phòng {stub.From:dd/MM} - {stub.To:dd/MM/yyyy} ({stub.Days} ngày × {Fmt.Money(form.MonthlyRent)} ÷ 30 = {Fmt.Vnd(stub.Rent)}), điện, nước thu ở kỳ chốt {stub.To:dd/MM/yyyy}."
                : $"1 tháng tiền phòng trả trước khi nhận phòng đúng ngày chốt {lease.StartDate:dd/MM/yyyy}, tính cho {stub.To:dd/MM} - {stub.To.AddMonths(1):dd/MM/yyyy}. Từ kỳ chốt {stub.To.AddMonths(1):dd/MM/yyyy} thu đủ tháng + điện + nước.",
            CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Bills.Add(bill);

        room.Status = RoomStatus.Occupied;
        room.Rent = form.MonthlyRent;
        await db.SaveChangesAsync();

        if (form.DepositPaid > 0)
        {
            var dep = new MoneyTransaction
            {
                AccountId = settings.PersonalAccountId, Date = form.PaymentDate.Date, Amount = form.DepositPaid, Category = TxCategory.Deposit,
                Description = $"Thu cọc phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Transactions.Add(dep);
            await db.SaveChangesAsync();
            AttachmentService.Attach(db, AttachmentOwner.Transaction, dep.Id, form.DepositReceipts, user);
        }
        if (form.FirstPaymentPaid > 0)
        {
            var pay = new MoneyTransaction
            {
                AccountId = settings.BusinessAccountId, Date = form.PaymentDate.Date, Amount = form.FirstPaymentPaid, Category = TxCategory.BillPayment,
                Description = $"Thu 1 tháng tiền phòng khi nhận phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, BillId = bill.Id, CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Transactions.Add(pay);
            await db.SaveChangesAsync();
            AttachmentService.Attach(db, AttachmentOwner.Transaction, pay.Id, form.PaymentReceipts, user);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return lease.Id;
    }

    public async Task<Lease?> GetLeaseAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Leases.AsNoTracking()
            .Include(l => l.Room).ThenInclude(r => r.Block)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant)
            .Include(l => l.Penalties)
            .Include(l => l.Bills).ThenInclude(b => b.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == leaseId);
    }

    /// <summary>Cập nhật danh sách người ở: thêm người mới, cập nhật thông tin, người bỏ khỏi danh sách được ghi ngày chuyển đi.</summary>
    public async Task SaveTenantsAsync(int leaseId, List<TenantInput> inputs, DateTime changeDate)
    {
        var list = inputs.Where(t => !t.IsEmpty).ToList();
        ValidateTenants(list);
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.Include(l => l.Room).Include(l => l.Tenants).ThenInclude(t => t.Tenant).FirstAsync(l => l.Id == leaseId);
        if (lease.Status != LeaseStatus.Active) throw new InvalidOperationException("Hợp đồng đã kết thúc.");
        if (list.Count > lease.Room.MaxOccupants) throw new InvalidOperationException($"Phòng tối đa {lease.Room.MaxOccupants} người.");

        var keep = new HashSet<int>();
        foreach (var input in list)
        {
            var other = await FindActiveRoomOfAsync(input.IdentityCard, leaseId);
            if (other != null) throw new InvalidOperationException($"{input.FullName} đang ở phòng {other}.");
            var tenant = await UpsertTenantAsync(db, input);
            await db.SaveChangesAsync();
            keep.Add(tenant.Id);
            var link = lease.Tenants.FirstOrDefault(t => t.TenantId == tenant.Id);
            if (link == null) lease.Tenants.Add(new LeaseTenant { TenantId = tenant.Id, MoveInDate = changeDate.Date });
            else link.MoveOutDate = null;
        }
        foreach (var link in lease.Tenants.Where(t => !keep.Contains(t.TenantId) && t.MoveOutDate == null))
            link.MoveOutDate = changeDate.Date;

        var active = lease.Tenants.Where(t => t.MoveOutDate == null).ToList();
        if (!active.Any(t => t.IsRepresentative))
        {
            foreach (var t in lease.Tenants) t.IsRepresentative = false;
            var firstId = list.Select(i => i.IdentityCard).First();
            var rep = active.FirstOrDefault(t => (t.Tenant?.IdentityCard ?? "") == firstId) ?? active.FirstOrDefault();
            if (rep != null) rep.IsRepresentative = true;
        }
        await db.SaveChangesAsync();
    }

    public async Task SetContractScanAsync(int leaseId, PendingFile file, string? user)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.FindAsync(leaseId) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        lease.ContractScanPath = file.Path;
        AttachmentService.Attach(db, AttachmentOwner.Lease, leaseId, [file with { Kind = AttachmentKind.ContractScan }], user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateLeaseTermsAsync(int leaseId, decimal monthlyRent, decimal deposit, string? note)
    {
        if (monthlyRent <= 0 || deposit < 0) throw new InvalidOperationException("Số tiền không hợp lệ.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var lease = await db.Leases.Include(l => l.Room).FirstAsync(l => l.Id == leaseId);
        lease.MonthlyRent = monthlyRent;
        lease.Deposit = deposit;
        lease.Note = note;
        lease.Room.Rent = monthlyRent;
        await db.SaveChangesAsync();
    }

    /// <summary>Tiền cọc đã thu (trừ phần đã trả lại) của hợp đồng.</summary>
    public async Task<decimal> GetDepositHeldAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Transactions.Where(t => t.LeaseId == leaseId && (t.Category == TxCategory.Deposit || t.Category == TxCategory.DepositRefund))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
    }

    /// <summary>Thu cọc sau khi nhận phòng (khách chưa nộp đủ cọc lúc vào).</summary>
    public async Task CollectDepositAsync(int leaseId, decimal amount, DateTime date, IEnumerable<PendingFile> receipts, string? user)
    {
        if (amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstAsync();
        var lease = await db.Leases.Include(l => l.Room).FirstAsync(l => l.Id == leaseId);
        if (lease.Status != LeaseStatus.Active) throw new InvalidOperationException("Hợp đồng đã kết thúc.");
        var tx = new MoneyTransaction
        {
            AccountId = settings.PersonalAccountId, Date = date.Date, Amount = amount, Category = TxCategory.Deposit,
            Description = $"Thu cọc phòng {lease.Room.Code}", RoomId = lease.RoomId, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        AttachmentService.Attach(db, AttachmentOwner.Transaction, tx.Id, receipts, user);
        await db.SaveChangesAsync();
    }

    // ============================ TRẢ PHÒNG ============================

    public async Task<CheckoutPreview> PreviewCheckoutAsync(CheckoutForm form)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var lease = await db.Leases.AsNoTracking().Include(l => l.Room).ThenInclude(r => r.Furniture).ThenInclude(f => f.FurnitureItem)
            .Include(l => l.Room).ThenInclude(r => r.Block).ThenInclude(b => b.DefaultFurniture)
            .Include(l => l.Tenants)
            .FirstAsync(l => l.Id == form.LeaseId);
        var bills = await db.Bills.AsNoTracking().Where(b => b.LeaseId == lease.Id)
            .Select(b => new { Bill = b, Paid = b.Payments.Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();

        int used = Math.Max(0, form.Kwh);
        var unpaid = bills.Where(b => b.Bill.TotalAmount - b.Paid > 0).Select(b => (b.Bill, b.Bill.TotalAmount - b.Paid)).ToList();

        var items = await db.FurnitureItems.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Name);
        var roomQty = lease.Room.Furniture.ToDictionary(f => f.FurnitureItemId, f => f.Quantity);
        var defQty = lease.Room.Block.DefaultFurniture.ToDictionary(f => f.FurnitureItemId, f => f.Quantity);
        var warnings = new List<string>();
        foreach (var id in roomQty.Keys.Union(defQty.Keys).Union(form.FurnitureActual.Keys))
        {
            int expected = roomQty.GetValueOrDefault(id), actual = form.FurnitureActual.TryGetValue(id, out var a) ? a : expected, def = defQty.GetValueOrDefault(id);
            var name = items.GetValueOrDefault(id, "?");
            if (actual != expected) warnings.Add($"{name}: thực tế {actual}, cấu hình phòng {expected}");
            else if (actual != def) warnings.Add($"{name}: phòng có {actual}, mặc định dãy {def}");
        }

        // Cọc thực giữ = tiền cọc đã thu − đã trả, không lấy số ghi trên hợp đồng (có thể chưa thu đủ).
        var held = await db.Transactions.Where(t => t.LeaseId == lease.Id && (t.Category == TxCategory.Deposit || t.Category == TxCategory.DepositRefund))
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        // Kỳ nào đã đến ngày thu (không sau ngày trả) mà chưa lập phiếu thì tính đủ tháng tiền phòng và nước vào phiếu trả phòng.
        int persons = Math.Max(1, lease.Tenants.Count(t => t.MoveOutDate == null));
        var missing = new List<MissingPeriod>();
        for (var d = new DateTime(lease.StartDate.Year, lease.StartDate.Month, 1); d <= form.EndDate.Date; d = d.AddMonths(1))
        {
            var due = RoomService.BillingDate(d.Month, d.Year, settings.BillingDay);
            if (due > form.EndDate.Date || !RoomService.NeedsMonthlyBill(lease, d.Month, d.Year, settings.BillingDay)) continue;
            if (bills.Any(b => b.Bill.Kind == BillKind.Monthly && b.Bill.Month == d.Month && b.Bill.Year == d.Year)) continue;
            var (from, to, rent) = RoomService.IsFirstBillingMonth(lease.StartDate, d.Month, d.Year, settings.BillingDay)
                ? (lease.StartDate, due, CalculateStubPeriod(lease.MonthlyRent, lease.StartDate, settings.BillingDay).Rent)
                : (due, due.AddMonths(1), lease.MonthlyRent);
            missing.Add(new MissingPeriod(d.Month, d.Year, from, to, rent, persons * settings.WaterPricePerPerson, persons));
        }

        // Chỉ trả lại cọc khi khách ở đủ số tháng tối thiểu (Cấu hình, mặc định 3 tháng); ở ít hơn thì cọc không hoàn trả.
        int minMonths = Math.Max(0, settings.MinStayMonthsForDepositRefund);
        var eligibleFrom = lease.StartDate.AddMonths(minMonths);
        bool refundable = minMonths == 0 || form.EndDate.Date >= eligibleFrom;
        int stayDays = Math.Max(0, (form.EndDate.Date - lease.StartDate).Days);

        // Khách xin ở thêm vài ngày để thu xếp: tiền phòng theo ngày = giá tháng ÷ 30, làm tròn nghìn.
        int extraDays = Math.Max(0, form.ExtraDays);
        decimal dayRate = RoundMoney(lease.MonthlyRent / 30m);
        decimal extraStay = RoundMoney(lease.MonthlyRent * extraDays / 30m);

        return new CheckoutPreview(Math.Max(0, held), used, used * settings.ElectricityPrice, form.Penalties.Sum(p => p.Amount), form.OtherAmount,
            unpaid.Sum(u => u.Item2), unpaid, warnings, missing, refundable, minMonths, eligibleFrom, stayDays, extraDays, dayRate, extraStay);
    }

    public async Task CheckoutAsync(CheckoutForm form, string? user)
    {
        if (form.DepositRefund < 0 || form.Collected < 0) throw new InvalidOperationException("Số tiền không được âm.");
        if (form.Penalties.Any(p => p.Amount < 0 || (p.Amount > 0 && string.IsNullOrWhiteSpace(p.Description))))
            throw new InvalidOperationException("Mỗi khoản phạt cần nội dung và số tiền không âm.");

        if (form.Kwh < 0) throw new InvalidOperationException("Số điện đã dùng không được âm.");
        if (form.ExtraDays < 0 || form.ExtraDays > 60) throw new InvalidOperationException("Số ngày ở thêm phải từ 0 đến 60.");
        var preview = await PreviewCheckoutAsync(form);
        if (form.DepositRefund > preview.Deposit) throw new InvalidOperationException("Số tiền trả cọc lớn hơn tiền cọc.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.FirstAsync();
        var lease = await db.Leases.Include(l => l.Room).ThenInclude(r => r.Furniture).Include(l => l.Tenants).FirstAsync(l => l.Id == form.LeaseId);
        if (lease.Status != LeaseStatus.Active) throw new InvalidOperationException("Hợp đồng đã kết thúc.");
        if (form.EndDate.Date < lease.StartDate) throw new InvalidOperationException("Ngày trả phòng trước ngày nhận phòng.");
        if (form.EndDate.Date > DateTime.Today.AddDays(7)) throw new InvalidOperationException("Ngày trả phòng quá xa trong tương lai, kiểm tra lại.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var date = form.EndDate.Date;
        var penalties = form.Penalties.Where(p => p.Amount > 0).ToList();

        // Phiếu quyết toán: điện kỳ cuối + phạt + khoản khác + tiền phòng, nước đủ tháng của kỳ đã đến ngày thu mà chưa lập phiếu
        Bill? moveOut = null;
        decimal moveOutTotal = preview.Electricity + preview.Penalties + form.OtherAmount + preview.MissingRent + preview.MissingWater + preview.ExtraStay;
        if (preview.Electricity != 0 || penalties.Count > 0 || form.OtherAmount != 0 || preview.Missing.Count > 0 || preview.ExtraStay != 0)
        {
            var notes = preview.Missing.Select(m => $"Kỳ {m.Month:00}/{m.Year} chưa lập phiếu, tính đủ tháng: phòng {Fmt.Vnd(m.Rent)}, nước {m.PersonCount} người {Fmt.Vnd(m.Water)}")
                .Append(preview.ExtraStay != 0 ? $"Ở thêm {preview.ExtraDays} ngày sau {date:dd/MM/yyyy}: {preview.ExtraDays} × {Fmt.Money(lease.MonthlyRent)} ÷ 30 = {Fmt.Vnd(preview.ExtraStay)}" : null)
                .Concat(penalties.Select(p => $"{p.Description}: {Fmt.Vnd(p.Amount)}"))
                .Append(form.OtherAmount != 0 ? $"{form.OtherNote ?? "Khoản khác"}: {Fmt.Vnd(form.OtherAmount)}" : null).Where(s => s != null);
            moveOut = new Bill
            {
                LeaseId = lease.Id, Kind = BillKind.MoveOut, Month = date.Month, Year = date.Year,
                PeriodFrom = preview.Missing.Count > 0 ? preview.Missing.Min(m => m.From) : date,
                PeriodTo = preview.Missing.Count > 0 ? preview.Missing.Max(m => m.To) : date,
                RentAmount = preview.MissingRent + preview.ExtraStay, WaterAmount = preview.MissingWater, PersonCount = preview.Missing.Count > 0 ? preview.Missing[0].PersonCount : 0,
                ElectricityKwh = preview.UsedKwh, ElectricityAmount = preview.Electricity,
                OtherAmount = preview.Penalties + form.OtherAmount, TotalAmount = moveOutTotal,
                Note = string.Join("; ", notes),
                CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Bills.Add(moveOut);
        }
        foreach (var p in penalties) lease.Penalties.Add(new LeasePenalty { Description = p.Description.Trim(), Amount = p.Amount });
        await db.SaveChangesAsync();

        // Danh sách khoản phải thu theo thứ tự: phiếu cũ còn nợ, rồi phiếu quyết toán
        var dues = preview.UnpaidList.Select(u => (u.Bill.Id, u.Due)).ToList();
        if (moveOut != null && moveOut.TotalAmount > 0) dues.Add((moveOut.Id, moveOut.TotalAmount));

        // Trả phòng: mọi khoản tiền (khấu trừ cọc, cọc không hoàn trả, khách nộp thêm, trả cọc) ghi ở TK cá nhân (yêu cầu 19/10/2026).
        var acct = settings.PersonalAccountId;
        var room = lease.Room;

        // 1) Khấu trừ cọc vào các khoản phải thu: trong TK cá nhân ghi "dùng cọc" (giảm cọc giữ hộ) và tiền thu của từng phiếu (doanh thu),
        //    số dư tài khoản không đổi. Ở chưa đủ tháng tối thiểu thì cọc không trừ vào các khoản: khách nộp riêng, cọc là doanh thu (1b).
        decimal applied = preview.DepositRefundable ? Math.Min(preview.Deposit - form.DepositRefund, dues.Sum(d => d.Due)) : 0;
        if (applied > 0)
        {
            var group = Guid.NewGuid();
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = acct, Date = date, Amount = -applied, Category = TxCategory.DepositRefund, TransferGroup = group,
                Description = $"Khấu trừ cọc phòng {room.Code} khi trả phòng", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
            });
            decimal left = applied;
            for (int i = 0; i < dues.Count && left > 0; i++)
            {
                var part = Math.Min(left, dues[i].Due);
                db.Transactions.Add(new MoneyTransaction
                {
                    AccountId = acct, Date = date, Amount = part, Category = TxCategory.BillPayment, TransferGroup = group,
                    Description = $"Khấu trừ cọc phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, BillId = dues[i].Id, CreatedAt = DateTime.Now, CreatedBy = user
                });
                dues[i] = (dues[i].Id, dues[i].Due - part);
                left -= part;
            }
        }

        // 1b) Phần cọc giữ lại ngoài khấu trừ (ở chưa đủ tháng tối thiểu nên không hoàn trả, hoặc chủ nhà quyết định giữ): là doanh thu,
        //     ghi trong TK cá nhân: "dùng cọc" và thu khác, số dư không đổi.
        decimal forfeited = preview.Deposit - form.DepositRefund - applied;
        if (forfeited > 0)
        {
            var group = Guid.NewGuid();
            var why = preview.DepositRefundable ? "" : $" (ở {preview.StayDays} ngày, chưa đủ {preview.MinStayMonths} tháng)";
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = acct, Date = date, Amount = -forfeited, Category = TxCategory.DepositRefund, TransferGroup = group,
                Description = $"Cọc không hoàn trả phòng {room.Code}{why}", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
            });
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = acct, Date = date, Amount = forfeited, Category = TxCategory.OtherIncome, TransferGroup = group,
                Description = $"Cọc không hoàn trả phòng {room.Code}{why}", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
            });
        }

        // 2) Tiền khách nộp thêm (vào TK cá nhân)
        if (form.Collected > 0)
        {
            decimal left = form.Collected;
            for (int i = 0; i < dues.Count && left > 0; i++)
            {
                if (dues[i].Due <= 0) continue;
                var part = Math.Min(left, dues[i].Due);
                db.Transactions.Add(new MoneyTransaction
                {
                    AccountId = acct, Date = date, Amount = part, Category = TxCategory.BillPayment,
                    Description = $"Thu khi trả phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, BillId = dues[i].Id, CreatedAt = DateTime.Now, CreatedBy = user
                });
                dues[i] = (dues[i].Id, dues[i].Due - part);
                left -= part;
            }
            if (left > 0)
                db.Transactions.Add(new MoneyTransaction
                {
                    AccountId = acct, Date = date, Amount = left, Category = TxCategory.OtherIncome,
                    Description = $"Thu thêm khi trả phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
                });
        }

        // 3) Trả cọc (chi từ TK cá nhân)
        if (form.DepositRefund > 0)
        {
            var refund = new MoneyTransaction
            {
                AccountId = settings.PersonalAccountId, Date = date, Amount = -form.DepositRefund, Category = TxCategory.DepositRefund,
                Description = $"Trả cọc phòng {room.Code}", RoomId = room.Id, LeaseId = lease.Id, CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Transactions.Add(refund);
            await db.SaveChangesAsync();
            AttachmentService.Attach(db, AttachmentOwner.Transaction, refund.Id, form.RefundReceipts, user);
        }

        // 4) Chốt hợp đồng, phòng, đồ đạc
        lease.Status = LeaseStatus.Ended;
        lease.EndDate = date;
        lease.CheckoutElectricity = preview.Electricity;
        lease.CheckoutPenalty = preview.Penalties;
        lease.CheckoutOther = form.OtherAmount;
        lease.DepositRefunded = form.DepositRefund;
        lease.CheckoutCollected = form.Collected;
        lease.CheckoutNote = form.Note;
        lease.FurnitureWarnings = preview.FurnitureWarnings.Count > 0 ? string.Join("; ", preview.FurnitureWarnings) : null;
        foreach (var t in lease.Tenants.Where(t => t.MoveOutDate == null)) t.MoveOutDate = date;

        room.Status = RoomStatus.Empty;
        if (form.UpdateRoomFurniture && form.FurnitureActual.Count > 0)
            SetupService.SyncFurniture(room.Furniture, form.FurnitureActual, (id, q) => new RoomFurniture { RoomId = room.Id, FurnitureItemId = id, Quantity = q },
                x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);

        AttachmentService.Attach(db, AttachmentOwner.Lease, lease.Id, form.Photos.Select(p => p with { Kind = AttachmentKind.CheckoutPhoto }), user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // ============================ DANH SÁCH NGƯỜI Ở ============================

    public async Task<List<ResidentRow>> ListResidentsAsync(bool currentOnly, string? search)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.LeaseTenants.AsNoTracking().AsQueryable();
        if (currentOnly) q = q.Where(lt => lt.MoveOutDate == null && lt.Lease.Status == LeaseStatus.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(lt => lt.Tenant.FullName.Contains(s) || lt.Tenant.IdentityCard.Contains(s) || lt.Lease.Room.Code.Contains(s) || (lt.Tenant.Phone != null && lt.Tenant.Phone.Contains(s)));
        }
        var rows = await q.OrderBy(lt => lt.Lease.Room.Block.Code).ThenBy(lt => lt.Lease.Room.Floor).ThenBy(lt => lt.Lease.Room.Number)
            .ThenByDescending(lt => lt.IsRepresentative).ThenBy(lt => lt.MoveInDate)
            .Select(lt => new { lt.LeaseId, lt.Lease.Room.Code, lt.Tenant, lt.MoveInDate, lt.MoveOutDate, lt.IsRepresentative })
            .Take(2000).ToListAsync();
        return rows.Select(r => new ResidentRow(r.LeaseId, r.Code, r.Tenant.FullName, r.Tenant.IdentityCard, r.Tenant.BirthDate, r.Tenant.Gender, r.Tenant.Phone,
            r.Tenant.Ethnicity, r.Tenant.Nationality, r.Tenant.PermanentAddress, r.Tenant.VehiclePlate, r.MoveInDate, r.MoveOutDate, r.IsRepresentative,
            r.Tenant.Portrait ?? r.Tenant.PhotoList.FirstOrDefault())).ToList();
    }
}
