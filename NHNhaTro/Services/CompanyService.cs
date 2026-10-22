using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Thêm thời hạn (hợp đồng gốc hoặc phụ lục) cho hợp đồng công ty.</summary>
public class TermForm
{
    public int Id { get; set; }
    public string DocumentNo { get; set; } = "";
    public DateTime? SignedDate { get; set; }
    public DateTime FromDate { get; set; } = DateTime.Today;
    public DateTime ToDate { get; set; } = DateTime.Today.AddYears(1).AddDays(-1);
    public decimal RentPerRoom { get; set; }
    public decimal DepositPerRoom { get; set; }
    public string? RoomCodes { get; set; }
    public string? Note { get; set; }
    /// <summary>Đặt giá thuê, cọc của các phòng đang gắn theo thời hạn này.</summary>
    public bool ApplyToLeases { get; set; } = true;
}

/// <summary>Thu gộp một lần chuyển khoản cho nhiều phiếu (VD 5 phiếu tiền phòng quý của công ty).</summary>
public class CollectForm
{
    public List<int> BillIds { get; set; } = [];
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int AccountId { get; set; }
    public string? Note { get; set; }
    public List<PendingFile> Receipts { get; set; } = [];
}

public record PeriodBill(Bill Bill, string RoomCode, decimal Paid)
{
    public decimal Due => Math.Max(0, Bill.TotalAmount - Paid);
}

/// <summary>Một kỳ tiền phòng kèm các phiếu đã lập; chưa lập thì Estimate là số dự tính theo các phòng đang gắn.</summary>
public record PeriodStatus(ContractPeriod Period, ContractTerm Term, List<PeriodBill> Bills, decimal Estimate)
{
    public bool Generated => Bills.Count > 0;
    public decimal Total => Generated ? Bills.Sum(b => b.Bill.TotalAmount) : Estimate;
    public decimal Paid => Bills.Sum(b => b.Paid);
    public decimal Due => Bills.Sum(b => b.Due);
    public bool Overdue(DateTime today) => Generated && Due > 0 && today.Date > Period.PayTo;
}

public record CompanyRoom(Lease Lease, string RoomCode, int PersonCount, string? Representative, decimal DepositHeld, decimal Outstanding);

public class CompanyDetails
{
    public required CompanyContract Contract { get; init; }
    public ContractTerm? CurrentTerm { get; init; }
    public List<PeriodStatus> Periods { get; init; } = [];
    public List<CompanyRoom> Rooms { get; init; } = [];
    /// <summary>Phiếu tháng (điện, nước) của các phòng trong kỳ đang xem.</summary>
    public List<PeriodBill> MonthlyBills { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public decimal DepositShortage => Rooms.Sum(r => Math.Max(0, r.Lease.Deposit - r.DepositHeld));
}

public record CompanySummary(int Id, string CompanyName, string ShortName, bool IsActive, int RoomCount, string? CurrentTerm, decimal Due);

/// <summary>Nhắc kỳ tiền phòng của hợp đồng công ty: còn nợ (Overdue nếu quá hạn nộp) hoặc sắp đến kỳ.</summary>
public record CompanyReminder(int ContractId, string ShortName, ContractPeriod Period, decimal Amount, bool Overdue, bool Upcoming);

public record AttachableLease(int LeaseId, string RoomCode, string? Representative, decimal MonthlyRent);

public class CompanyService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);
    /// <summary>Lập phiếu kỳ trước ngày bắt đầu kỳ bao nhiêu ngày thì nhắc "sắp đến kỳ".</summary>
    public const int UpcomingDays = 15;

    // ============================ TÍNH TOÁN ============================

    /// <summary>Tiền phòng cho khoảng [from, to): số tháng tròn × giá + ngày lẻ × giá ÷ 30 (làm tròn nghìn).</summary>
    public static (decimal Amount, string Detail) RentForSpan(DateTime from, DateTime to, decimal monthlyRent)
    {
        from = from.Date; to = to.Date;
        int m = 0;
        while (from.AddMonths(m + 1) <= to) m++;
        int days = Math.Max(0, (to - from.AddMonths(m)).Days);
        var amount = m * monthlyRent + LeaseService.RoundMoney(monthlyRent * days / 30m);
        var parts = new List<string>();
        if (m > 0) parts.Add($"{m} tháng × {Fmt.Money(monthlyRent)}");
        if (days > 0) parts.Add($"{days} ngày × {Fmt.Money(monthlyRent)} ÷ 30");
        return (amount, parts.Count > 0 ? string.Join(" + ", parts) : "0");
    }

    /// <summary>Diễn giải tiền phòng của phiếu kỳ hợp đồng: "Kỳ 10/12/2026 → 09/03/2027: 3 tháng × 2.200.000".</summary>
    public static string ContractRentDetail(Bill bill, decimal monthlyRent) =>
        $"Kỳ {bill.PeriodFrom:dd/MM/yyyy} → {bill.PeriodTo.AddDays(-1):dd/MM/yyyy}: {RentForSpan(bill.PeriodFrom, bill.PeriodTo, monthlyRent).Detail}";

    /// <summary>Chia thời hạn [from, to] thành các kỳ N tháng; hạn nộp mặc định 15 ngày đầu kỳ. Kỳ đã qua hạn nộp đánh dấu đã thu ngoài phần mềm.</summary>
    public static List<ContractPeriod> BuildPeriods(DateTime from, DateTime toInclusive, int months, DateTime today)
    {
        months = Math.Clamp(months, 1, 24);
        var end = toInclusive.Date.AddDays(1);
        var list = new List<ContractPeriod>();
        for (int k = 1; k <= 200; k++)
        {
            var start = from.Date.AddMonths(months * (k - 1));
            if (start >= end) break;
            var stop = from.Date.AddMonths(months * k);
            if (stop > end) stop = end;
            var payTo = start.AddDays(15);
            list.Add(new ContractPeriod { Number = k, From = start, To = stop, PayFrom = start, PayTo = payTo, SettledOutside = payTo < today.Date });
        }
        return list;
    }

    /// <summary>Thời hạn đang hiệu lực vào ngày cho trước; không có thì lấy thời hạn mới nhất.</summary>
    public static ContractTerm? CurrentTerm(IEnumerable<ContractTerm> terms, DateTime date) =>
        terms.Where(t => t.FromDate <= date.Date && date.Date <= t.ToDate).OrderByDescending(t => t.FromDate).FirstOrDefault()
        ?? terms.OrderByDescending(t => t.FromDate).FirstOrDefault();

    /// <summary>
    /// Lập phiếu tiền phòng cho các kỳ đã bắt đầu (ngày đầu kỳ ≤ hôm nay), chưa đánh dấu thu ngoài phần mềm:
    /// mỗi phòng đang gắn hợp đồng một phiếu. Phòng vào giữa kỳ tính từ ngày vào (tháng tròn + ngày lẻ ÷ 30).
    /// Gọi khi mở trang chủ, sơ đồ, hợp đồng công ty.
    /// </summary>
    public static async Task EnsureBillsAsync(ApplicationDbContext db, DateTime today)
    {
        await EnsureLock.WaitAsync();
        try
        {
            var periods = await db.ContractPeriods.Include(p => p.Term).ThenInclude(t => t.CompanyContract)
                .Where(p => !p.SettledOutside && p.From <= today.Date && p.Term.CompanyContract.IsActive).ToListAsync();
            if (periods.Count == 0) return;
            var contractIds = periods.Select(p => p.Term.CompanyContractId).Distinct().ToList();
            var leases = await db.Leases.Include(l => l.Room).Include(l => l.Tenants)
                .Where(l => l.Status == LeaseStatus.Active && l.CompanyContractId != null && contractIds.Contains(l.CompanyContractId.Value)).ToListAsync();
            if (leases.Count == 0) return;
            var periodIds = periods.Select(p => p.Id).ToList();
            var existing = (await db.Bills.Where(b => b.ContractPeriodId != null && periodIds.Contains(b.ContractPeriodId.Value))
                .Select(b => new { b.ContractPeriodId, b.LeaseId }).ToListAsync())
                .Select(x => (x.ContractPeriodId!.Value, x.LeaseId)).ToHashSet();

            bool added = false;
            foreach (var p in periods)
            {
                var c = p.Term.CompanyContract;
                foreach (var l in leases.Where(l => l.CompanyContractId == c.Id && l.StartDate.Date < p.To && !existing.Contains((p.Id, l.Id))))
                {
                    var from = l.StartDate.Date > p.From ? l.StartDate.Date : p.From;
                    var (amount, detail) = RentForSpan(from, p.To, p.Term.RentPerRoom);
                    db.Bills.Add(new Bill
                    {
                        LeaseId = l.Id, Kind = BillKind.ContractRent, Month = p.From.Month, Year = p.From.Year, ContractPeriodId = p.Id,
                        PeriodFrom = from, PeriodTo = p.To, RentAmount = amount, TotalAmount = amount,
                        PersonCount = l.Tenants.Count(t => t.MoveOutDate == null),
                        Note = $"Tiền phòng kỳ {p.Number} theo {p.Term.DocumentNo} ({c.ShortName}): {from:dd/MM/yyyy} → {p.To.AddDays(-1):dd/MM/yyyy}, {detail}. Hạn nộp {p.PayFrom:dd/MM} – {p.PayTo:dd/MM/yyyy}.",
                        CreatedAt = DateTime.Now, CreatedBy = "Tự động"
                    });
                    added = true;
                }
            }
            if (added) await db.SaveChangesAsync();
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    // ============================ ĐỌC ============================

    public async Task<List<CompanySummary>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureBillsAsync(db, DateTime.Today);
        var list = await db.CompanyContracts.AsNoTracking().Include(c => c.Terms).OrderByDescending(c => c.IsActive).ThenBy(c => c.ShortName).ToListAsync();
        var counts = await db.Leases.Where(l => l.Status == LeaseStatus.Active && l.CompanyContractId != null)
            .GroupBy(l => l.CompanyContractId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var dues = await db.Bills.Where(b => b.Kind == BillKind.ContractRent && b.Lease.CompanyContractId != null)
            .Select(b => new { C = b.Lease.CompanyContractId!.Value, Due = b.TotalAmount - (b.Payments.Sum(p => (decimal?)p.Amount) ?? 0) })
            .ToListAsync();
        return list.Select(c =>
        {
            var t = CurrentTerm(c.Terms, DateTime.Today);
            return new CompanySummary(c.Id, c.CompanyName, c.ShortName, c.IsActive, counts.GetValueOrDefault(c.Id),
                t == null ? null : $"{t.DocumentNo}: {t.FromDate:dd/MM/yyyy} → {t.ToDate:dd/MM/yyyy}, {Fmt.Money(t.RentPerRoom)}/phòng",
                dues.Where(d => d.C == c.Id).Sum(d => Math.Max(0, d.Due)));
        }).ToList();
    }

    public async Task<CompanyDetails?> GetAsync(int id, int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureBillsAsync(db, DateTime.Today);
        var c = await db.CompanyContracts.AsNoTracking().Include(x => x.Terms).ThenInclude(t => t.Periods).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return null;
        c.Terms = c.Terms.OrderBy(t => t.FromDate).ToList();
        foreach (var t in c.Terms) t.Periods = t.Periods.OrderBy(p => p.From).ToList();
        var term = CurrentTerm(c.Terms, DateTime.Today);

        var leases = await db.Leases.AsNoTracking().Where(l => l.CompanyContractId == id && l.Status == LeaseStatus.Active)
            .Include(l => l.Room).ThenInclude(r => r.Block).Include(l => l.Tenants).ThenInclude(t => t.Tenant)
            .OrderBy(l => l.Room.Block.Code).ThenBy(l => l.Room.Floor).ThenBy(l => l.Room.Number).AsSplitQuery().ToListAsync();
        var leaseIds = leases.Select(l => l.Id).ToList();
        var held = await db.Transactions.Where(t => t.LeaseId != null && leaseIds.Contains(t.LeaseId.Value) && (t.Category == TxCategory.Deposit || t.Category == TxCategory.DepositRefund))
            .GroupBy(t => t.LeaseId!.Value).Select(g => new { g.Key, Sum = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);

        var periodIds = c.Terms.SelectMany(t => t.Periods).Select(p => p.Id).ToList();
        var bills = await db.Bills.AsNoTracking()
            .Where(b => (b.ContractPeriodId != null && periodIds.Contains(b.ContractPeriodId.Value)) || leaseIds.Contains(b.LeaseId))
            .Select(b => new { Bill = b, Room = b.Lease.Room.Code, b.Lease.Room.Block.Code, b.Lease.Room.Floor, b.Lease.Room.Number, Paid = b.Payments.Sum(p => (decimal?)p.Amount) ?? 0 })
            .ToListAsync();
        var ordered = bills.OrderBy(b => b.Code).ThenBy(b => b.Floor).ThenBy(b => b.Number).Select(b => new PeriodBill(b.Bill, b.Room, b.Paid)).ToList();

        var periods = c.Terms.SelectMany(t => t.Periods.Select(p =>
        {
            var pb = ordered.Where(b => b.Bill.ContractPeriodId == p.Id).ToList();
            decimal estimate = p.SettledOutside ? 0 : leases.Where(l => l.StartDate.Date < p.To)
                .Sum(l => RentForSpan(l.StartDate.Date > p.From ? l.StartDate.Date : p.From, p.To, t.RentPerRoom).Amount);
            return new PeriodStatus(p, t, pb, estimate);
        })).ToList();

        var rooms = leases.Select(l =>
        {
            var active = l.Tenants.Where(t => t.MoveOutDate == null).ToList();
            var rep = (active.FirstOrDefault(t => t.IsRepresentative) ?? active.FirstOrDefault())?.Tenant.FullName;
            var outstanding = ordered.Where(b => b.Bill.LeaseId == l.Id).Sum(b => b.Due);
            return new CompanyRoom(l, l.Room.Code, active.Count, rep, held.GetValueOrDefault(l.Id), outstanding);
        }).ToList();

        var warnings = new List<string>();
        if (term != null)
        {
            var codes = term.RoomList;
            var attached = rooms.Select(r => r.RoomCode).ToList();
            var missing = codes.Except(attached).ToList();
            var extra = attached.Except(codes).ToList();
            if (codes.Count > 0 && missing.Count > 0) warnings.Add($"Theo {term.DocumentNo} còn phòng {string.Join(", ", missing)} chưa gắn vào hợp đồng (nhận phòng chọn hợp đồng công ty, hoặc gắn phòng đang thuê).");
            if (codes.Count > 0 && extra.Count > 0) warnings.Add($"Phòng {string.Join(", ", extra)} đang gắn hợp đồng nhưng không có trong {term.DocumentNo}.");
            var diffRent = rooms.Where(r => r.Lease.MonthlyRent != term.RentPerRoom).Select(r => r.RoomCode).ToList();
            if (diffRent.Count > 0) warnings.Add($"Giá thuê phòng {string.Join(", ", diffRent)} khác giá {Fmt.Vnd(term.RentPerRoom)} của {term.DocumentNo}.");
            if (term.ToDate < DateTime.Today) warnings.Add($"{term.DocumentNo} đã hết hạn {term.ToDate:dd/MM/yyyy}: thêm phụ lục gia hạn hoặc trả phòng.");
            else if ((term.ToDate - DateTime.Today).TotalDays <= 45) warnings.Add($"{term.DocumentNo} hết hạn {term.ToDate:dd/MM/yyyy}: chuẩn bị phụ lục gia hạn.");
        }
        else warnings.Add("Chưa có thời hạn hợp đồng: thêm hợp đồng gốc hoặc phụ lục để phần mềm lập phiếu tiền phòng theo kỳ.");

        return new CompanyDetails
        {
            Contract = c, CurrentTerm = term, Periods = periods, Rooms = rooms, Warnings = warnings,
            MonthlyBills = ordered.Where(b => b.Bill.Kind == BillKind.Monthly && b.Bill.Month == month && b.Bill.Year == year && leaseIds.Contains(b.Bill.LeaseId)).ToList()
        };
    }

    /// <summary>Các kỳ còn nợ hoặc sắp đến (để nhắc ở trang chủ).</summary>
    public async Task<List<CompanyReminder>> GetRemindersAsync()
    {
        var today = DateTime.Today;
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureBillsAsync(db, today);
        var contracts = await db.CompanyContracts.AsNoTracking().Where(c => c.IsActive).Include(c => c.Terms).ThenInclude(t => t.Periods).AsSplitQuery().ToListAsync();
        if (contracts.Count == 0) return [];
        var bills = await db.Bills.Where(b => b.Kind == BillKind.ContractRent && b.ContractPeriodId != null)
            .Select(b => new { b.ContractPeriodId, Due = b.TotalAmount - (b.Payments.Sum(p => (decimal?)p.Amount) ?? 0) }).ToListAsync();
        var leaseCount = await db.Leases.Where(l => l.Status == LeaseStatus.Active && l.CompanyContractId != null)
            .GroupBy(l => l.CompanyContractId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var list = new List<CompanyReminder>();
        foreach (var c in contracts)
            foreach (var t in c.Terms)
                foreach (var p in t.Periods.Where(p => !p.SettledOutside).OrderBy(p => p.From))
                {
                    var pb = bills.Where(b => b.ContractPeriodId == p.Id).ToList();
                    if (pb.Count > 0)
                    {
                        var due = pb.Sum(b => Math.Max(0, b.Due));
                        if (due > 0) list.Add(new CompanyReminder(c.Id, c.ShortName, p, due, today > p.PayTo, false));
                    }
                    else if (p.From > today && (p.From - today).TotalDays <= UpcomingDays && leaseCount.GetValueOrDefault(c.Id) > 0)
                        list.Add(new CompanyReminder(c.Id, c.ShortName, p, leaseCount[c.Id] * RentForSpan(p.From, p.To, t.RentPerRoom).Amount, false, true));
                }
        return list.OrderBy(r => r.Period.PayTo).ToList();
    }

    public async Task<List<AttachableLease>> ListAttachableLeasesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Leases.AsNoTracking().Where(l => l.Status == LeaseStatus.Active && l.CompanyContractId == null)
            .OrderBy(l => l.Room.Block.Code).ThenBy(l => l.Room.Floor).ThenBy(l => l.Room.Number)
            .Select(l => new { l.Id, l.Room.Code, l.MonthlyRent, Rep = l.Tenants.Where(t => t.MoveOutDate == null).OrderByDescending(t => t.IsRepresentative).Select(t => t.Tenant.FullName).FirstOrDefault() })
            .ToListAsync();
        return rows.Select(r => new AttachableLease(r.Id, r.Code, r.Rep, r.MonthlyRent)).ToList();
    }

    public async Task<List<CompanyContract>> ListActiveAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CompanyContracts.AsNoTracking().Where(c => c.IsActive).Include(c => c.Terms).OrderBy(c => c.ShortName).ToListAsync();
    }

    // ============================ GHI ============================

    public async Task<int> SaveContractAsync(CompanyContract input, string? user)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName)) throw new InvalidOperationException("Nhập tên công ty.");
        if (string.IsNullOrWhiteSpace(input.ShortName)) throw new InvalidOperationException("Nhập tên ngắn (VD: Horn) để hiện trên phòng, phiếu.");
        if (input.RentMonthsPerPeriod is < 1 or > 12) throw new InvalidOperationException("Số tháng mỗi kỳ tiền phòng từ 1 đến 12.");
        if (input.WaterPerRoom < 0) throw new InvalidOperationException("Tiền nước không được âm.");
        if (input.MaxOccupantsPerRoom is < 1 or > 20) throw new InvalidOperationException("Số người tối đa mỗi phòng từ 1 đến 20.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = input.Id == 0 ? null : await db.CompanyContracts.FindAsync(input.Id);
        if (c == null)
        {
            c = new CompanyContract { CreatedAt = DateTime.Now, CreatedBy = user };
            db.CompanyContracts.Add(c);
        }
        c.CompanyName = input.CompanyName.Trim();
        c.ShortName = input.ShortName.Trim();
        c.TaxCode = input.TaxCode?.Trim();
        c.Address = input.Address?.Trim();
        c.Representative = input.Representative?.Trim();
        c.RepresentativeTitle = input.RepresentativeTitle?.Trim();
        c.Phone = input.Phone?.Trim();
        c.BankAccount = input.BankAccount?.Trim();
        c.ContractNo = input.ContractNo?.Trim();
        c.SignedDate = input.SignedDate?.Date;
        c.RentMonthsPerPeriod = input.RentMonthsPerPeriod;
        c.WaterPerRoom = input.WaterPerRoom;
        c.MaxOccupantsPerRoom = input.MaxOccupantsPerRoom;
        c.IsActive = input.IsActive;
        c.Note = input.Note;
        await db.SaveChangesAsync();
        return c.Id;
    }

    public async Task AddTermAsync(int contractId, TermForm f)
    {
        if (string.IsNullOrWhiteSpace(f.DocumentNo)) throw new InvalidOperationException("Nhập số hợp đồng / phụ lục.");
        if (f.ToDate.Date < f.FromDate.Date) throw new InvalidOperationException("Ngày hết hạn trước ngày bắt đầu.");
        if (f.ToDate.Date > f.FromDate.Date.AddYears(10)) throw new InvalidOperationException("Thời hạn quá dài, kiểm tra lại ngày.");
        if (f.RentPerRoom <= 0 || f.DepositPerRoom < 0) throw new InvalidOperationException("Giá thuê phải lớn hơn 0, cọc không âm.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.CompanyContracts.Include(x => x.Terms).FirstOrDefaultAsync(x => x.Id == contractId) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        var overlap = c.Terms.FirstOrDefault(t => t.FromDate <= f.ToDate.Date && f.FromDate.Date <= t.ToDate);
        if (overlap != null) throw new InvalidOperationException($"Thời hạn trùng với {overlap.DocumentNo} ({overlap.FromDate:dd/MM/yyyy} → {overlap.ToDate:dd/MM/yyyy}). Xóa thời hạn đó trước nếu nhập sai.");
        var term = new ContractTerm
        {
            CompanyContractId = c.Id, DocumentNo = f.DocumentNo.Trim(), SignedDate = f.SignedDate?.Date, FromDate = f.FromDate.Date, ToDate = f.ToDate.Date,
            RentPerRoom = f.RentPerRoom, DepositPerRoom = f.DepositPerRoom, RoomCodes = NormalizeRooms(f.RoomCodes), Note = f.Note,
            Periods = BuildPeriods(f.FromDate, f.ToDate, c.RentMonthsPerPeriod, DateTime.Today)
        };
        var unknown = term.RoomList.Except(await db.Rooms.Select(r => r.Code).ToListAsync()).ToList();
        if (unknown.Count > 0) throw new InvalidOperationException($"Không có phòng {string.Join(", ", unknown)} trong phần mềm.");
        db.ContractTerms.Add(term);
        if (f.ApplyToLeases) await ApplyTermAsync(db, c, term);
        await db.SaveChangesAsync();
    }

    /// <summary>Sửa thông tin thời hạn (không đổi ngày; muốn đổi ngày thì xóa và thêm lại khi chưa có phiếu).</summary>
    public async Task UpdateTermAsync(TermForm f)
    {
        if (string.IsNullOrWhiteSpace(f.DocumentNo)) throw new InvalidOperationException("Nhập số hợp đồng / phụ lục.");
        if (f.RentPerRoom <= 0 || f.DepositPerRoom < 0) throw new InvalidOperationException("Giá thuê phải lớn hơn 0, cọc không âm.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.ContractTerms.Include(x => x.CompanyContract).FirstOrDefaultAsync(x => x.Id == f.Id) ?? throw new InvalidOperationException("Không tìm thấy thời hạn.");
        t.DocumentNo = f.DocumentNo.Trim();
        t.SignedDate = f.SignedDate?.Date;
        t.RentPerRoom = f.RentPerRoom;
        t.DepositPerRoom = f.DepositPerRoom;
        t.RoomCodes = NormalizeRooms(f.RoomCodes);
        t.Note = f.Note;
        if (f.ApplyToLeases) await ApplyTermAsync(db, t.CompanyContract, t);
        await db.SaveChangesAsync();
    }

    public async Task DeleteTermAsync(int termId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.ContractTerms.Include(x => x.Periods).FirstOrDefaultAsync(x => x.Id == termId) ?? throw new InvalidOperationException("Không tìm thấy thời hạn.");
        var ids = t.Periods.Select(p => p.Id).ToList();
        if (await db.Bills.AnyAsync(b => b.ContractPeriodId != null && ids.Contains(b.ContractPeriodId.Value)))
            throw new InvalidOperationException("Thời hạn này đã có phiếu tiền phòng: không xóa được.");
        db.ContractTerms.Remove(t);
        await db.SaveChangesAsync();
    }

    /// <summary>Sửa hạn nộp, ghi chú của một kỳ; đánh dấu đã thu ngoài phần mềm thì xóa các phiếu chưa thu của kỳ.</summary>
    public async Task UpdatePeriodAsync(int periodId, DateTime payFrom, DateTime payTo, bool settledOutside, string? note)
    {
        if (payTo.Date < payFrom.Date) throw new InvalidOperationException("Hạn nộp phải sau ngày bắt đầu nộp.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var p = await db.ContractPeriods.FindAsync(periodId) ?? throw new InvalidOperationException("Không tìm thấy kỳ.");
        p.PayFrom = payFrom.Date;
        p.PayTo = payTo.Date;
        p.Note = note;
        if (settledOutside && !p.SettledOutside)
        {
            var bills = await db.Bills.Include(b => b.Payments).Where(b => b.ContractPeriodId == periodId).ToListAsync();
            if (bills.Any(b => b.Payments.Count > 0)) throw new InvalidOperationException("Kỳ này đã có khoản thu trên phần mềm: không đánh dấu thu ngoài được.");
            db.Bills.RemoveRange(bills);
        }
        p.SettledOutside = settledOutside;
        await db.SaveChangesAsync();
    }

    /// <summary>Gắn các phòng đang thuê vào hợp đồng công ty: giá, cọc theo thời hạn hiện hành, tối đa số người theo hợp đồng.</summary>
    public async Task AttachLeasesAsync(int contractId, List<int> leaseIds)
    {
        if (leaseIds.Count == 0) throw new InvalidOperationException("Chọn ít nhất một phòng.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.CompanyContracts.Include(x => x.Terms).FirstOrDefaultAsync(x => x.Id == contractId) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        var leases = await db.Leases.Include(l => l.Room).Where(l => leaseIds.Contains(l.Id)).ToListAsync();
        if (leases.Any(l => l.Status != LeaseStatus.Active)) throw new InvalidOperationException("Chỉ gắn được phòng đang thuê.");
        var term = CurrentTerm(c.Terms, DateTime.Today);
        foreach (var l in leases)
        {
            l.CompanyContractId = c.Id;
            l.Room.MaxOccupants = c.MaxOccupantsPerRoom;
            if (term != null) ApplyTerm(l, term);
        }
        await db.SaveChangesAsync();
    }

    public async Task DetachLeaseAsync(int leaseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Leases.FindAsync(leaseId) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng phòng.");
        l.CompanyContractId = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Thu một lần chuyển khoản cho nhiều phiếu, chia lần lượt theo thứ tự phòng; mỗi phiếu một dòng thu, cùng chứng từ.</summary>
    public async Task<decimal> CollectBillsAsync(CollectForm f, string label, string? user)
    {
        if (f.BillIds.Count == 0) throw new InvalidOperationException("Không có phiếu nào để thu.");
        if (f.Amount <= 0) throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Accounts.AnyAsync(a => a.Id == f.AccountId)) throw new InvalidOperationException("Chọn tài khoản nhận tiền.");
        var bills = await db.Bills.Where(b => f.BillIds.Contains(b.Id))
            .Select(b => new { b.Id, b.LeaseId, b.Lease.RoomId, Room = b.Lease.Room.Code, Block = b.Lease.Room.Block.Code, b.Lease.Room.Floor, b.Lease.Room.Number,
                Due = b.TotalAmount - (b.Payments.Sum(p => (decimal?)p.Amount) ?? 0) })
            .ToListAsync();
        var open = bills.Where(b => b.Due > 0).OrderBy(b => b.Block).ThenBy(b => b.Floor).ThenBy(b => b.Number).ToList();
        var total = open.Sum(b => b.Due);
        if (total <= 0) throw new InvalidOperationException("Các phiếu này đã thu đủ.");
        if (f.Amount > total) throw new InvalidOperationException($"Số tiền thu ({Fmt.Vnd(f.Amount)}) lớn hơn số còn phải thu ({Fmt.Vnd(total)}). Phần thừa ghi ở Thu chi > Thu khác.");

        await using var tx = await db.Database.BeginTransactionAsync();
        decimal left = f.Amount;
        var created = new List<MoneyTransaction>();
        foreach (var b in open)
        {
            if (left <= 0) break;
            var part = Math.Min(left, b.Due);
            var t = new MoneyTransaction
            {
                AccountId = f.AccountId, Date = f.Date.Date, Amount = part, Category = TxCategory.BillPayment,
                Description = $"{label} - phòng {b.Room} (thu gộp {Fmt.Vnd(f.Amount)})" + (string.IsNullOrWhiteSpace(f.Note) ? "" : " - " + f.Note.Trim()),
                RoomId = b.RoomId, LeaseId = b.LeaseId, BillId = b.Id, CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Transactions.Add(t);
            created.Add(t);
            left -= part;
        }
        await db.SaveChangesAsync();
        foreach (var t in created) AttachmentService.Attach(db, AttachmentOwner.Transaction, t.Id, f.Receipts, user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return f.Amount;
    }

    /// <summary>Ghi phần cọc còn thiếu của từng phòng (cọc theo hợp đồng − cọc đã ghi) vào tài khoản chọn.</summary>
    public async Task<decimal> CollectDepositsAsync(int contractId, int accountId, DateTime date, List<PendingFile> receipts, string? user)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var c = await db.CompanyContracts.FindAsync(contractId) ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");
        if (!await db.Accounts.AnyAsync(a => a.Id == accountId)) throw new InvalidOperationException("Chọn tài khoản.");
        var leases = await db.Leases.Include(l => l.Room).Where(l => l.CompanyContractId == contractId && l.Status == LeaseStatus.Active).ToListAsync();
        var ids = leases.Select(l => l.Id).ToList();
        var held = await db.Transactions.Where(t => t.LeaseId != null && ids.Contains(t.LeaseId.Value) && (t.Category == TxCategory.Deposit || t.Category == TxCategory.DepositRefund))
            .GroupBy(t => t.LeaseId!.Value).Select(g => new { g.Key, Sum = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        var created = new List<MoneyTransaction>();
        foreach (var l in leases.OrderBy(l => l.Room.Code))
        {
            var missing = l.Deposit - held.GetValueOrDefault(l.Id);
            if (missing <= 0) continue;
            var t = new MoneyTransaction
            {
                AccountId = accountId, Date = date.Date, Amount = missing, Category = TxCategory.Deposit,
                Description = $"Thu cọc phòng {l.Room.Code} (HĐ {c.ShortName})", RoomId = l.RoomId, LeaseId = l.Id, CreatedAt = DateTime.Now, CreatedBy = user
            };
            db.Transactions.Add(t);
            created.Add(t);
        }
        if (created.Count == 0) throw new InvalidOperationException("Các phòng đã đủ cọc theo hợp đồng.");
        await db.SaveChangesAsync();
        foreach (var t in created) AttachmentService.Attach(db, AttachmentOwner.Transaction, t.Id, receipts, user);
        await db.SaveChangesAsync();
        return created.Sum(t => t.Amount);
    }

    // ============================ PHỤ TRỢ ============================

    private static string? NormalizeRooms(string? codes)
    {
        if (string.IsNullOrWhiteSpace(codes)) return null;
        var list = codes.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant()).Distinct().ToList();
        return list.Count == 0 ? null : string.Join(", ", list);
    }

    private static void ApplyTerm(Lease l, ContractTerm term)
    {
        l.MonthlyRent = term.RentPerRoom;
        l.Room.Rent = term.RentPerRoom;
        l.Deposit = term.DepositPerRoom;
    }

    private static async Task ApplyTermAsync(ApplicationDbContext db, CompanyContract c, ContractTerm term)
    {
        var leases = await db.Leases.Include(l => l.Room).Where(l => l.CompanyContractId == c.Id && l.Status == LeaseStatus.Active).ToListAsync();
        foreach (var l in leases) ApplyTerm(l, term);
    }
}
