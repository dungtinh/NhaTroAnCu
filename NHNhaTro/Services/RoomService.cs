using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Màu thẻ phòng trên sơ đồ (giữ nguyên quy ước của bản cũ).</summary>
public static class RoomColor
{
    public const string Gray = "gray";     // Phòng trống
    public const string Blue = "blue";     // Đang thuê, chưa có phiếu báo
    public const string Green = "green";   // Đã thanh toán đủ
    public const string Yellow = "yellow"; // Thanh toán một phần
    public const string Orange = "orange"; // Chưa thanh toán
    public const string Red = "red";       // Hết hạn hợp đồng
    public const string Purple = "purple"; // Có người ở không hợp đồng
}

public record RoomCard(Room Room, string Color, string TenantName, bool IsNearingEnd, bool IsExpired, DateTime? ContractEndDate, int? ContractId);

public record RoomDashboard(List<RoomCard> Rooms, int Total, int Rented, int Empty, int NearExpired, int Expired, decimal OccupancyRate);

public class MonthlyPaymentHistory
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<UtilityBill> Bills { get; set; } = [];
    public List<IncomeExpense> Payments { get; set; } = [];
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal MonthBalance { get; set; }
    /// <summary>Số dư lũy kế: dương là tiền thừa (đặt cọc), âm là còn nợ.</summary>
    public decimal CumulativeDeposit { get; set; }
}

public class RoomDetails
{
    public required Room Room { get; init; }
    public Contract? Contract { get; init; }
    public decimal ActualPrice { get; init; }
    public List<Tenant> Tenants { get; init; } = [];
    public UtilityBill? CurrentBill { get; init; }
    public decimal AmountDue { get; init; }
    public List<MonthlyPaymentHistory> PaymentHistory { get; init; } = [];
}

public class RoomHistory
{
    public required Room Room { get; init; }
    public List<ContractHistoryItem> Contracts { get; init; } = [];
    public List<TenantHistoryItem> Tenants { get; init; } = [];
    public List<PaymentHistoryItem> Payments { get; init; } = [];
    public ContractHistoryItem? CurrentContract { get; init; }
    public int TotalMonthsRented { get; set; }
    public int OccupancyRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageMonthlyRevenue { get; set; }
    public int AverageContractDuration { get; set; }
    public decimal TotalDebt { get; set; }
    public int PaymentCompleteRate { get; set; }
}

public record ContractHistoryItem(int Id, DateTime StartDate, DateTime EndDate, string Status, decimal MonthlyRent, string? ContractType,
    List<string> TenantNames, decimal TotalPaid, decimal TotalDebt, int PaymentRate);

public record TenantHistoryItem(int Id, string FullName, string IdentityCard, string? PhoneNumber, string? Gender, DateTime MoveInDate, DateTime? MoveOutDate, bool IsCurrent);

public record PaymentHistoryItem(int ContractId, int Month, int Year, decimal BillAmount, decimal PaidAmount, decimal Remaining, string Status, DateTime? PaymentDate);

public class RoomService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>
    /// Hợp đồng "hiện tại" của phòng: hợp đồng mới nhất còn Active hoặc đã quá hạn nhưng chưa kết thúc (Expired).
    /// </summary>
    public static IQueryable<Contract> CurrentContractsOfRoom(ApplicationDbContext db, int roomId) =>
        db.Contracts.Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired)
                                && c.ContractRooms.Any(cr => cr.RoomId == roomId))
                    .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id);

    public async Task<RoomDashboard> GetDashboardAsync(DateTime filterDate)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        int month = filterDate.Month, year = filterDate.Year;

        var rooms = await db.Rooms.AsNoTracking()
            .Include(r => r.ContractRooms).ThenInclude(cr => cr.Contract).ThenInclude(c => c.Company)
            .Include(r => r.ContractTenants).ThenInclude(ct => ct.Tenant)
            .AsSplitQuery()
            .ToListAsync();

        var bills = await db.UtilityBills.AsNoTracking()
            .Where(b => b.Month == month && b.Year == year)
            .ToListAsync();

        var paidByContract = await db.IncomeExpenses.AsNoTracking()
            .Where(ie => ie.ContractId != null && ie.Category.Name == SystemCategories.RoomPayment && ie.Category.Type == CategoryType.Income
                         && ie.TransactionDate.Month == month && ie.TransactionDate.Year == year)
            .GroupBy(ie => ie.ContractId!.Value)
            .Select(g => new { ContractId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ContractId, x => x.Paid);

        var cards = rooms.Select(room => BuildCard(room, filterDate, bills, paidByContract)).ToList();

        int total = cards.Count;
        int rented = cards.Count(c => c.Color is RoomColor.Blue or RoomColor.Green or RoomColor.Yellow or RoomColor.Orange);
        return new RoomDashboard(cards, total, rented, total - rented,
            cards.Count(c => c.IsNearingEnd), cards.Count(c => c.IsExpired),
            total > 0 ? (decimal)rented / total : 0);
    }

    private static RoomCard BuildCard(Room room, DateTime filterDate, List<UtilityBill> monthBills, Dictionary<int, decimal> paidByContract)
    {
        var contract = room.ContractRooms
            .Select(cr => cr.Contract)
            .Where(c => c.Status is ContractStatus.Active or ContractStatus.Expired && c.StartDate <= filterDate)
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .FirstOrDefault();

        if (contract == null)
        {
            return room.IsOccupied
                ? new RoomCard(room, RoomColor.Purple, "Có người ở", false, false, null, null)
                : new RoomCard(room, RoomColor.Gray, "", false, false, null, null);
        }

        string tenantName = room.ContractTenants
            .Where(ct => ct.ContractId == contract.Id)
            .OrderBy(ct => ct.CreatedAt)
            .Select(ct => ct.Tenant.FullName)
            .FirstOrDefault() ?? contract.Company?.CompanyName ?? "";

        if (contract.EndDate < filterDate)
        {
            return new RoomCard(room, RoomColor.Red, string.IsNullOrEmpty(tenantName) ? "(Hết HĐ)" : tenantName + " (Hết HĐ)",
                false, true, contract.EndDate, contract.Id);
        }

        bool nearing = (contract.EndDate - filterDate).TotalDays <= 31;

        // Hợp đồng công ty có nhiều phòng: so tổng tiền đã thu của hợp đồng với tổng phiếu báo của hợp đồng trong tháng.
        var contractBills = monthBills.Where(b => b.ContractId == contract.Id).ToList();
        string color;
        if (contractBills.Count == 0)
        {
            color = RoomColor.Blue;
        }
        else
        {
            decimal mustPay = contractBills.Sum(b => b.TotalAmount);
            decimal paid = paidByContract.GetValueOrDefault(contract.Id);
            color = paid >= mustPay ? RoomColor.Green : paid > 0 ? RoomColor.Yellow : RoomColor.Orange;
        }

        return new RoomCard(room, color, tenantName, nearing, false, contract.EndDate, contract.Id);
    }

    public async Task<RoomDetails?> GetDetailsAsync(int roomId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return null;

        var contract = await CurrentContractsOfRoom(db, roomId).AsNoTracking()
            .Include(c => c.ContractRooms)
            .Include(c => c.Company)
            .Include(c => c.ContractExtensionHistories)
            .FirstOrDefaultAsync();

        if (contract == null)
            return new RoomDetails { Room = room, ActualPrice = room.DefaultPrice };

        var now = DateTime.Today;
        var tenants = await db.ContractTenants.AsNoTracking()
            .Where(ct => ct.ContractId == contract.Id && ct.RoomId == roomId)
            .OrderBy(ct => ct.CreatedAt)
            .Select(ct => ct.Tenant)
            .ToListAsync();

        var bills = await db.UtilityBills.AsNoTracking()
            .Where(b => b.ContractId == contract.Id)
            .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month)
            .ToListAsync();

        var payments = await db.IncomeExpenses.AsNoTracking()
            .Where(ie => ie.ContractId == contract.Id && ie.Category.Name == SystemCategories.RoomPayment)
            .OrderBy(ie => ie.TransactionDate).ThenBy(ie => ie.Id)
            .ToListAsync();

        var history = BuildPaymentHistory(bills, payments);
        decimal due = bills.Sum(b => b.TotalAmount) - payments.Sum(p => p.Amount);

        return new RoomDetails
        {
            Room = room,
            Contract = contract,
            ActualPrice = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId)?.PriceAgreed ?? room.DefaultPrice,
            Tenants = tenants,
            CurrentBill = bills.FirstOrDefault(b => b.RoomId == roomId && b.Month == now.Month && b.Year == now.Year),
            AmountDue = due > 0 ? due : 0,
            PaymentHistory = history
        };
    }

    /// <summary>
    /// Gộp phiếu báo và tiền thu theo tháng, tính số dư lũy kế theo thứ tự thời gian (bản cũ cộng dồn
    /// theo thứ tự giảm dần nên số dư lũy kế bị ngược; ở đây cộng dồn từ tháng cũ nhất).
    /// </summary>
    public static List<MonthlyPaymentHistory> BuildPaymentHistory(List<UtilityBill> bills, List<IncomeExpense> payments)
    {
        var keys = bills.Select(b => (b.Year, b.Month))
            .Concat(payments.Select(p => (p.TransactionDate.Year, p.TransactionDate.Month)))
            .Distinct()
            .OrderBy(k => k.Year).ThenBy(k => k.Month)
            .ToList();

        var result = new List<MonthlyPaymentHistory>();
        decimal cumulative = 0;
        foreach (var (year, month) in keys)
        {
            var monthBills = bills.Where(b => b.Year == year && b.Month == month).ToList();
            var monthPayments = payments.Where(p => p.TransactionDate.Year == year && p.TransactionDate.Month == month).ToList();
            decimal billed = monthBills.Sum(b => b.TotalAmount);
            decimal paid = monthPayments.Sum(p => p.Amount);
            cumulative += paid - billed;
            result.Add(new MonthlyPaymentHistory
            {
                Year = year,
                Month = month,
                Bills = monthBills,
                Payments = monthPayments,
                TotalBilled = billed,
                TotalPaid = paid,
                MonthBalance = paid - billed,
                CumulativeDeposit = cumulative
            });
        }

        result.Reverse();
        return result;
    }

    public async Task<Room?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> SaveAsync(Room input)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        input.Area = (input.Area ?? "A").Trim().ToUpperInvariant();
        if (input.Area.Length != 1) throw new InvalidOperationException("Dãy phải là 1 ký tự (ví dụ A, B).");
        if (string.IsNullOrWhiteSpace(input.Name))
            input.Name = $"P{input.Area}{input.Floor}{input.RoomNumber:00}";
        input.Name = input.Name.Trim();
        if (input.Name.Length > 10) throw new InvalidOperationException("Tên phòng tối đa 10 ký tự.");

        if (await db.Rooms.AnyAsync(r => r.Name == input.Name && r.Id != input.Id))
            throw new InvalidOperationException($"Đã có phòng tên {input.Name}.");

        if (input.Id == 0)
        {
            db.Rooms.Add(input);
        }
        else
        {
            var room = await db.Rooms.FindAsync(input.Id) ?? throw new InvalidOperationException("Không tìm thấy phòng.");
            db.Entry(room).CurrentValues.SetValues(input);
        }
        await db.SaveChangesAsync();
        return input.Id;
    }

    public async Task<bool> ToggleOccupiedAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy phòng.");
        room.IsOccupied = !room.IsOccupied;
        await db.SaveChangesAsync();
        return room.IsOccupied;
    }

    public async Task<RoomHistory?> GetHistoryAsync(int roomId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return null;

        var contracts = await db.Contracts.AsNoTracking()
            .Where(c => c.ContractRooms.Any(cr => cr.RoomId == roomId))
            .Include(c => c.ContractTenants.Where(ct => ct.RoomId == roomId)).ThenInclude(ct => ct.Tenant)
            .Include(c => c.ContractRooms)
            .OrderByDescending(c => c.StartDate)
            .AsSplitQuery()
            .ToListAsync();
        var contractIds = contracts.Select(c => c.Id).ToList();

        var bills = await db.UtilityBills.AsNoTracking()
            .Where(b => contractIds.Contains(b.ContractId) && b.RoomId == roomId)
            .ToListAsync();
        var payments = await db.IncomeExpenses.AsNoTracking()
            .Where(ie => ie.ContractId != null && contractIds.Contains(ie.ContractId.Value) && ie.Category.Name == SystemCategories.RoomPayment)
            .ToListAsync();

        var contractItems = contracts.Select(c =>
        {
            var cBills = bills.Where(b => b.ContractId == c.Id).Sum(b => b.TotalAmount);
            var cPaid = payments.Where(p => p.ContractId == c.Id).Sum(p => p.Amount);
            return new ContractHistoryItem(c.Id, c.StartDate, c.EndDate, c.Status,
                c.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId)?.PriceAgreed ?? c.PriceAgreed, c.ContractType,
                c.ContractTenants.Select(ct => ct.Tenant.FullName).ToList(),
                cPaid, cBills - cPaid, cBills > 0 ? (int)(cPaid / cBills * 100) : 100);
        }).ToList();

        var tenants = contracts
            .SelectMany(c => c.ContractTenants.Select(ct => new TenantHistoryItem(ct.Tenant.Id, ct.Tenant.FullName, ct.Tenant.IdentityCard,
                ct.Tenant.PhoneNumber, ct.Tenant.Gender, c.StartDate, c.Status == ContractStatus.Ended ? c.EndDate : null,
                c.Status != ContractStatus.Ended)))
            .GroupBy(t => t.Id).Select(g => g.OrderByDescending(t => t.MoveInDate).First())
            .ToList();

        var paymentItems = bills
            .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month)
            .Select(b =>
            {
                var monthPayments = payments.Where(p => p.ContractId == b.ContractId && p.TransactionDate.Month == b.Month && p.TransactionDate.Year == b.Year).ToList();
                var paid = monthPayments.Sum(p => p.Amount);
                var remaining = b.TotalAmount - paid;
                return new PaymentHistoryItem(b.ContractId, b.Month, b.Year, b.TotalAmount, paid, remaining,
                    remaining <= 0 ? "Paid" : paid > 0 ? "Partial" : "Unpaid",
                    monthPayments.OrderBy(p => p.TransactionDate).FirstOrDefault()?.TransactionDate);
            }).ToList();

        var current = contractItems.FirstOrDefault(c => c.Status is ContractStatus.Active or ContractStatus.Expired);
        var model = new RoomHistory { Room = room, Contracts = contractItems, Tenants = tenants, Payments = paymentItems, CurrentContract = current };

        var today = DateTime.Today;
        foreach (var c in contractItems)
        {
            var end = c.EndDate < today ? c.EndDate : today;
            if (end > c.StartDate) model.TotalMonthsRented += Math.Max(1, Fmt.MonthsBetween(c.StartDate, end));
        }

        var oneYearAgo = today.AddYears(-1);
        double occupiedDays = 0;
        foreach (var c in contractItems)
        {
            var start = c.StartDate < oneYearAgo ? oneYearAgo : c.StartDate;
            var end = c.EndDate > today ? today : c.EndDate;
            if (start < end) occupiedDays += (end - start).TotalDays;
        }
        model.OccupancyRate = Math.Min(100, (int)(occupiedDays / (today - oneYearAgo).TotalDays * 100));
        model.TotalRevenue = payments.Sum(p => p.Amount);
        if (model.TotalMonthsRented > 0) model.AverageMonthlyRevenue = model.TotalRevenue / model.TotalMonthsRented;
        if (contractItems.Count > 0) model.AverageContractDuration = model.TotalMonthsRented / contractItems.Count;
        model.TotalDebt = paymentItems.Sum(p => p.Remaining);
        var totalBilled = paymentItems.Sum(p => p.BillAmount);
        if (totalBilled > 0) model.PaymentCompleteRate = (int)(paymentItems.Sum(p => p.PaidAmount) / totalBilled * 100);
        return model;
    }
}
