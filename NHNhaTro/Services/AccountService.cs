using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public record AccountBalance(MoneyAccount Account, decimal Income, decimal Expense, decimal Balance);

public class LedgerFilter
{
    public int? AccountId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Category { get; set; }
    /// <summary>"in" thu, "out" chi.</summary>
    public string? Direction { get; set; }
    public string? Search { get; set; }
    public int? LeaseId { get; set; }
    public int? RoomId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}

public record LedgerRow(int Id, DateTime Date, string AccountName, decimal Amount, string Category, string? Description, string? RoomCode,
    int? LeaseId, int? BillId, int? RepairId, int? PurchaseId, Guid? TransferGroup, string? CreatedBy, DateTime CreatedAt, int AttachmentCount);

public record LedgerPage(List<LedgerRow> Rows, int Total, decimal SumIn, decimal SumOut);

public class ManualTransactionForm
{
    public int AccountId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    /// <summary>true = thu vào, false = chi ra.</summary>
    public bool IsIncome { get; set; } = true;
    public decimal Amount { get; set; }
    public string Category { get; set; } = TxCategory.OtherIncome;
    public string? Description { get; set; }
    public int? RoomId { get; set; }
}

public record MonthlyCategoryRow(string Category, decimal Income, decimal Expense);

/// <summary>Một tháng trong báo cáo quý/năm: thu, chi (không tính chuyển nội bộ, cọc giữ hộ).</summary>
public record PeriodPoint(int Month, int Year, decimal Income, decimal Expense)
{
    public decimal Profit => Income - Expense;
}

/// <summary>Điện nước trong kỳ báo cáo: tính cho khách theo phiếu và trả tổng cho điện lực, nước sạch.</summary>
public record UtilitySummary(int BilledKwh, decimal BilledElectricity, decimal BilledWater, int? PaidKwh, decimal PaidElectricity, decimal PaidWater);

public record ReportData(DateTime From, DateTime To, List<(MoneyAccount Account, decimal Opening, decimal Income, decimal Expense, decimal Closing)> Accounts,
    List<MonthlyCategoryRow> Categories, List<(string RoomCode, decimal Amount)> RevenueByRoom, List<PeriodPoint> Months, UtilitySummary Utility,
    decimal DepositIn, decimal DepositOut)
{
    public decimal Income => Months.Sum(m => m.Income);
    public decimal Expense => Months.Sum(m => m.Expense);
    public decimal Profit => Income - Expense;
}

public class AccountService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files)
{
    public async Task<List<AccountBalance>> GetBalancesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        var sums = await db.Transactions.GroupBy(t => t.AccountId).Select(g => new
        {
            Id = g.Key,
            In = g.Where(t => t.Amount > 0).Sum(t => (decimal?)t.Amount) ?? 0,
            Out = g.Where(t => t.Amount < 0).Sum(t => (decimal?)t.Amount) ?? 0
        }).ToDictionaryAsync(x => x.Id);
        return accounts.Select(a =>
        {
            var s = sums.GetValueOrDefault(a.Id);
            decimal i = s?.In ?? 0, o = -(s?.Out ?? 0);
            return new AccountBalance(a, i, o, a.OpeningBalance + i - o);
        }).ToList();
    }

    public async Task<LedgerPage> GetLedgerAsync(LedgerFilter f)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Transactions.AsNoTracking().AsQueryable();
        if (f.AccountId.HasValue) q = q.Where(t => t.AccountId == f.AccountId);
        if (f.From.HasValue) q = q.Where(t => t.Date >= f.From.Value.Date);
        if (f.To.HasValue) q = q.Where(t => t.Date <= f.To.Value.Date);
        if (!string.IsNullOrEmpty(f.Category)) q = q.Where(t => t.Category == f.Category);
        if (f.LeaseId.HasValue) q = q.Where(t => t.LeaseId == f.LeaseId);
        if (f.RoomId.HasValue) q = q.Where(t => t.RoomId == f.RoomId);
        if (f.Direction == "in") q = q.Where(t => t.Amount > 0);
        if (f.Direction == "out") q = q.Where(t => t.Amount < 0);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(t => (t.Description != null && t.Description.Contains(s)) || (t.Room != null && t.Room.Code.Contains(s)));
        }

        int total = await q.CountAsync();
        decimal sumIn = await q.Where(t => t.Amount > 0).SumAsync(t => (decimal?)t.Amount) ?? 0;
        decimal sumOut = -(await q.Where(t => t.Amount < 0).SumAsync(t => (decimal?)t.Amount) ?? 0);
        int size = Math.Clamp(f.PageSize, 10, 200);
        var rows = await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Skip((Math.Max(1, f.Page) - 1) * size).Take(size)
            .Select(t => new LedgerRow(t.Id, t.Date, t.Account.Name, t.Amount, t.Category, t.Description, t.Room != null ? t.Room.Code : null,
                t.LeaseId, t.BillId, t.RepairId, t.PurchaseId, t.TransferGroup, t.CreatedBy, t.CreatedAt,
                db.Attachments.Count(a => a.EntityType == AttachmentOwner.Transaction && a.EntityId == t.Id)))
            .ToListAsync();
        return new LedgerPage(rows, total, sumIn, sumOut);
    }

    public async Task<int> AddManualAsync(ManualTransactionForm form, IEnumerable<PendingFile> receipts, string? user)
    {
        if (form.Amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Accounts.AnyAsync(a => a.Id == form.AccountId)) throw new InvalidOperationException("Chọn tài khoản.");
        var tx = new MoneyTransaction
        {
            AccountId = form.AccountId, Date = form.Date.Date, Amount = form.IsIncome ? form.Amount : -form.Amount,
            Category = form.Category, Description = form.Description?.Trim(), RoomId = form.RoomId, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        AttachmentService.Attach(db, AttachmentOwner.Transaction, tx.Id, receipts, user);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    public async Task TransferAsync(int fromAccountId, int toAccountId, decimal amount, DateTime date, string? note, IEnumerable<PendingFile> receipts, string? user)
    {
        if (fromAccountId == toAccountId) throw new InvalidOperationException("Chọn hai tài khoản khác nhau.");
        if (amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var group = Guid.NewGuid();
        var desc = string.IsNullOrWhiteSpace(note) ? "Chuyển tiền nội bộ" : note.Trim();
        var outTx = new MoneyTransaction { AccountId = fromAccountId, Date = date.Date, Amount = -amount, Category = TxCategory.Transfer, Description = desc, TransferGroup = group, CreatedAt = DateTime.Now, CreatedBy = user };
        db.Transactions.Add(outTx);
        db.Transactions.Add(new MoneyTransaction { AccountId = toAccountId, Date = date.Date, Amount = amount, Category = TxCategory.Transfer, Description = desc, TransferGroup = group, CreatedAt = DateTime.Now, CreatedBy = user });
        await db.SaveChangesAsync();
        AttachmentService.Attach(db, AttachmentOwner.Transaction, outTx.Id, receipts, user);
        await db.SaveChangesAsync();
    }

    /// <summary>Xóa một dòng thu chi (chỉ Quản trị). Chuyển nội bộ thì xóa cả hai dòng.</summary>
    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var tx = await db.Transactions.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy giao dịch.");
        if (tx.RepairId != null || tx.PurchaseId != null)
            throw new InvalidOperationException("Giao dịch của phiếu sửa chữa/mua sắm: hãy xóa phiếu đó.");
        if (tx.UtilityBillId != null)
            throw new InvalidOperationException("Đây là tiền điện/nước tổng của kỳ: sửa hoặc xóa ở trang Ghi số điện.");
        var list = tx.TransferGroup != null ? await db.Transactions.Where(t => t.TransferGroup == tx.TransferGroup).ToListAsync() : [tx];
        var ids = list.Select(t => t.Id).ToList();
        var atts = await db.Attachments.Where(a => a.EntityType == AttachmentOwner.Transaction && ids.Contains(a.EntityId)).ToListAsync();
        db.Attachments.RemoveRange(atts);
        db.Transactions.RemoveRange(list);
        await db.SaveChangesAsync();
        foreach (var a in atts) files.Delete(a.Path);
    }

    /// <summary>Không phải doanh thu/chi phí: chuyển giữa hai tài khoản, cọc giữ hộ khách.</summary>
    private static bool IsNonOperating(string category) => category is TxCategory.Transfer or TxCategory.Deposit or TxCategory.DepositRefund;

    /// <summary>Báo cáo thu chi cho khoảng [from, to) (tháng, quý hoặc năm), kèm số liệu từng tháng.</summary>
    public async Task<ReportData> GetReportAsync(DateTime from, DateTime to)
    {
        from = from.Date; to = to.Date;
        await using var db = await dbFactory.CreateDbContextAsync();
        var accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        var before = await db.Transactions.Where(t => t.Date < from).GroupBy(t => t.AccountId).Select(g => new { g.Key, Sum = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        var txs = await db.Transactions.AsNoTracking().Include(t => t.Room).Where(t => t.Date >= from && t.Date < to).ToListAsync();

        var accRows = accounts.Select(a =>
        {
            var opening = a.OpeningBalance + before.GetValueOrDefault(a.Id);
            var inc = txs.Where(t => t.AccountId == a.Id && t.Amount > 0).Sum(t => t.Amount);
            var exp = -txs.Where(t => t.AccountId == a.Id && t.Amount < 0).Sum(t => t.Amount);
            return (a, opening, inc, exp, opening + inc - exp);
        }).ToList();

        var cats = txs.Where(t => t.Category != TxCategory.Transfer).GroupBy(t => t.Category)
            .Select(g => new MonthlyCategoryRow(g.Key, g.Where(t => t.Amount > 0).Sum(t => t.Amount), -g.Where(t => t.Amount < 0).Sum(t => t.Amount)))
            .OrderByDescending(r => r.Income + r.Expense).ToList();

        var byRoom = txs.Where(t => t.Amount > 0 && t.Room != null && t.Category is TxCategory.BillPayment or TxCategory.Rent or TxCategory.Electricity or TxCategory.Water or TxCategory.Penalty)
            .GroupBy(t => t.Room!.Code).Select(g => (g.Key, g.Sum(t => t.Amount))).OrderByDescending(x => x.Item2).ToList();

        var ops = txs.Where(t => !IsNonOperating(t.Category)).ToList();
        var months = new List<PeriodPoint>();
        for (var m = new DateTime(from.Year, from.Month, 1); m < to; m = m.AddMonths(1))
        {
            var inMonth = ops.Where(t => t.Date.Year == m.Year && t.Date.Month == m.Month).ToList();
            months.Add(new PeriodPoint(m.Month, m.Year, inMonth.Where(t => t.Amount > 0).Sum(t => t.Amount), -inMonth.Where(t => t.Amount < 0).Sum(t => t.Amount)));
        }

        // Điện nước: kỳ thu (tháng/năm của phiếu) nằm trong khoảng báo cáo
        int fromKey = from.Year * 12 + from.Month - 1, toKey = to.Year * 12 + to.Month - 1;
        var bills = await db.Bills.AsNoTracking().Where(b => b.Year * 12 + b.Month - 1 >= fromKey && b.Year * 12 + b.Month - 1 < toKey)
            .Select(b => new { b.ElectricityKwh, b.ElectricityAmount, b.WaterAmount }).ToListAsync();
        var utils = await db.UtilityBills.AsNoTracking().Where(u => u.Year * 12 + u.Month - 1 >= fromKey && u.Year * 12 + u.Month - 1 < toKey).ToListAsync();
        var utility = new UtilitySummary(bills.Sum(b => b.ElectricityKwh), bills.Sum(b => b.ElectricityAmount), bills.Sum(b => b.WaterAmount),
            utils.Any(u => u.ElectricityKwh != null) ? utils.Sum(u => u.ElectricityKwh ?? 0) : null, utils.Sum(u => u.ElectricityAmount), utils.Sum(u => u.WaterAmount));

        decimal depIn = txs.Where(t => t.Category == TxCategory.Deposit).Sum(t => t.Amount);
        decimal depOut = -txs.Where(t => t.Category == TxCategory.DepositRefund).Sum(t => t.Amount);
        return new ReportData(from, to, accRows, cats, byRoom, months, utility, depIn, depOut);
    }
}
