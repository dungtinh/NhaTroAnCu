using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Một dòng trên bảng thu tiền tháng (một phòng đang thuê).</summary>
public class BillingRow
{
    public int LeaseId { get; set; }
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string? Representative { get; set; }
    public int PersonCount { get; set; }
    public decimal Rent { get; set; }
    public int MeterStart { get; set; }
    public int? MeterEnd { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal Discount { get; set; }
    public string? Note { get; set; }
    public int? BillId { get; set; }
    public decimal Paid { get; set; }
    /// <summary>Nhận phòng trong tháng này: kỳ đầu đã thu tới ngày thu tháng sau, không lập phiếu tháng.</summary>
    public bool SkipThisMonth { get; set; }
    public decimal PreviousDebt { get; set; }

    public decimal Electricity(decimal price) => MeterEnd is int end && end >= MeterStart ? (end - MeterStart) * price : 0;
    public decimal Water(decimal perPerson) => PersonCount * perPerson;
    public decimal Total(decimal price, decimal perPerson) => Math.Max(0, Rent + Electricity(price) + Water(perPerson) + OtherAmount - Discount);
}

public record BillingSheet(int Month, int Year, DateTime DueDate, decimal ElectricityPrice, decimal WaterPerPerson, List<BillingRow> Rows);

public class PaymentForm
{
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int AccountId { get; set; }
    public string? Note { get; set; }
    public List<PendingFile> Receipts { get; set; } = [];
}

public class BillingService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<BillingSheet> GetSheetAsync(int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var due = new DateTime(year, month, Math.Min(s.BillingDay, DateTime.DaysInMonth(year, month)));

        var leases = await db.Leases.AsNoTracking().Where(l => l.Status == LeaseStatus.Active)
            .Include(l => l.Room).ThenInclude(r => r.Block).Include(l => l.Tenants).ThenInclude(t => t.Tenant)
            .OrderBy(l => l.Room.Block.Code).ThenBy(l => l.Room.Floor).ThenBy(l => l.Room.Number).AsSplitQuery().ToListAsync();
        var ids = leases.Select(l => l.Id).ToList();
        var bills = await db.Bills.AsNoTracking().Where(b => ids.Contains(b.LeaseId))
            .Select(b => new { Bill = b, Paid = b.Payments.Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();

        var rows = leases.Select(l =>
        {
            var lb = bills.Where(b => b.Bill.LeaseId == l.Id).ToList();
            var current = lb.FirstOrDefault(b => b.Bill.Kind == BillKind.Monthly && b.Bill.Month == month && b.Bill.Year == year);
            var earlier = lb.Where(b => b.Bill.Id != current?.Bill.Id && (b.Bill.Year < year || (b.Bill.Year == year && b.Bill.Month <= month))).ToList();
            int meterStart = current?.Bill.MeterStart
                ?? earlier.Where(b => b.Bill.MeterEnd != null).OrderByDescending(b => b.Bill.Year).ThenByDescending(b => b.Bill.Month).ThenByDescending(b => b.Bill.Id)
                    .Select(b => b.Bill.MeterEnd).FirstOrDefault()
                ?? l.StartMeterReading;
            var active = l.Tenants.Where(t => t.MoveOutDate == null).ToList();
            return new BillingRow
            {
                LeaseId = l.Id, RoomId = l.RoomId, RoomCode = l.Room.Code,
                Representative = (active.FirstOrDefault(t => t.IsRepresentative) ?? active.FirstOrDefault())?.Tenant.FullName,
                PersonCount = current?.Bill.PersonCount ?? active.Count,
                Rent = current?.Bill.RentAmount ?? l.MonthlyRent,
                MeterStart = meterStart, MeterEnd = current?.Bill.MeterEnd,
                OtherAmount = current?.Bill.OtherAmount ?? 0, Discount = current?.Bill.Discount ?? 0, Note = current?.Bill.Note,
                BillId = current?.Bill.Id, Paid = current?.Paid ?? 0,
                SkipThisMonth = !RoomService.NeedsMonthlyBill(l, month, year),
                PreviousDebt = earlier.Sum(b => Math.Max(0, b.Bill.TotalAmount - b.Paid))
            };
        }).ToList();

        return new BillingSheet(month, year, due, s.ElectricityPrice, s.WaterPricePerPerson, rows);
    }

    /// <summary>Lập hoặc cập nhật phiếu tháng cho một phòng. Trả về Id phiếu.</summary>
    public async Task<int> SaveBillAsync(int month, int year, BillingRow row, string? user)
    {
        if (row.MeterEnd is not int end) throw new InvalidOperationException($"Phòng {row.RoomCode}: nhập số điện mới.");
        if (end < row.MeterStart) throw new InvalidOperationException($"Phòng {row.RoomCode}: số điện mới ({end}) nhỏ hơn số cũ ({row.MeterStart}).");
        if (row.Rent < 0 || row.OtherAmount < 0 || row.Discount < 0) throw new InvalidOperationException($"Phòng {row.RoomCode}: số tiền không được âm.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        var lease = await db.Leases.Include(l => l.Room).FirstAsync(l => l.Id == row.LeaseId);
        if (lease.Status != LeaseStatus.Active) throw new InvalidOperationException($"Phòng {row.RoomCode}: hợp đồng đã kết thúc.");

        var bill = await db.Bills.FirstOrDefaultAsync(b => b.LeaseId == lease.Id && b.Kind == BillKind.Monthly && b.Month == month && b.Year == year);
        if (bill == null)
        {
            bill = new Bill { LeaseId = lease.Id, Kind = BillKind.Monthly, Month = month, Year = year, CreatedAt = DateTime.Now, CreatedBy = user };
            db.Bills.Add(bill);
        }
        var from = new DateTime(year, month, Math.Min(s.BillingDay, DateTime.DaysInMonth(year, month)));
        bill.PeriodFrom = from;
        bill.PeriodTo = from.AddMonths(1);
        bill.RentAmount = row.Rent;
        bill.MeterStart = row.MeterStart;
        bill.MeterEnd = end;
        bill.ElectricityAmount = row.Electricity(s.ElectricityPrice);
        bill.PersonCount = row.PersonCount;
        bill.WaterAmount = row.Water(s.WaterPricePerPerson);
        bill.OtherAmount = row.OtherAmount;
        bill.Discount = row.Discount;
        bill.TotalAmount = row.Total(s.ElectricityPrice, s.WaterPricePerPerson);
        bill.Note = row.Note;

        // Số điện hiện tại của phòng = số mới nhất đã chốt
        var latestOther = await db.Bills.Where(b => b.LeaseId == lease.Id && b.Id != bill.Id && b.MeterEnd != null)
            .OrderByDescending(b => b.Year).ThenByDescending(b => b.Month).Select(b => new { b.Year, b.Month, b.MeterEnd }).FirstOrDefaultAsync();
        if (latestOther == null || latestOther.Year < year || (latestOther.Year == year && latestOther.Month <= month)) lease.Room.MeterReading = end;

        await db.SaveChangesAsync();
        return bill.Id;
    }

    public async Task<Bill?> GetBillAsync(int billId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Bills.AsNoTracking().Include(b => b.Payments).ThenInclude(p => p.Account)
            .Include(b => b.Lease).ThenInclude(l => l.Room)
            .Include(b => b.Lease).ThenInclude(l => l.Tenants).ThenInclude(t => t.Tenant)
            .AsSplitQuery().FirstOrDefaultAsync(b => b.Id == billId);
    }

    public async Task<int> RecordPaymentAsync(PaymentForm form, string? user)
    {
        if (form.Amount <= 0) throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var bill = await db.Bills.Include(b => b.Lease).ThenInclude(l => l.Room).FirstOrDefaultAsync(b => b.Id == form.BillId)
            ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
        if (!await db.Accounts.AnyAsync(a => a.Id == form.AccountId)) throw new InvalidOperationException("Chọn tài khoản nhận tiền.");
        var paid = await db.Transactions.Where(t => t.BillId == bill.Id).SumAsync(t => (decimal?)t.Amount) ?? 0;
        var due = bill.TotalAmount - paid;
        if (due <= 0) throw new InvalidOperationException("Phiếu này đã thu đủ.");
        if (form.Amount > due) throw new InvalidOperationException($"Số tiền thu ({Fmt.Vnd(form.Amount)}) lớn hơn số còn phải thu ({Fmt.Vnd(due)}). Nếu khách đưa thừa, ghi phần thừa ở Thu chi > Thu khác.");
        var room = bill.Lease.Room;
        var tx = new MoneyTransaction
        {
            AccountId = form.AccountId, Date = form.Date.Date, Amount = form.Amount, Category = TxCategory.BillPayment,
            Description = $"Thu tiền phòng {room.Code} {BillKind.Display(bill.Kind).ToLower(Fmt.Vi)} {bill.Month:00}/{bill.Year}" + (string.IsNullOrWhiteSpace(form.Note) ? "" : " - " + form.Note.Trim()),
            RoomId = room.Id, LeaseId = bill.LeaseId, BillId = bill.Id, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        AttachmentService.Attach(db, AttachmentOwner.Transaction, tx.Id, form.Receipts, user);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    /// <summary>Xóa phiếu (chỉ Quản trị, và phiếu chưa có khoản thu).</summary>
    public async Task DeleteBillAsync(int billId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var bill = await db.Bills.Include(b => b.Payments).FirstOrDefaultAsync(b => b.Id == billId) ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
        if (bill.Payments.Count > 0) throw new InvalidOperationException("Phiếu đã có khoản thu: xóa các khoản thu trước.");
        if (bill.Kind != BillKind.Monthly) throw new InvalidOperationException("Chỉ xóa được phiếu hằng tháng.");
        db.Bills.Remove(bill);
        await db.SaveChangesAsync();
    }

    public async Task<List<(Bill Bill, decimal Paid)>> GetUnpaidBillsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var list = await db.Bills.AsNoTracking().Include(b => b.Lease).ThenInclude(l => l.Room)
            .Select(b => new { Bill = b, Paid = b.Payments.Sum(p => (decimal?)p.Amount) ?? 0 })
            .Where(x => x.Bill.TotalAmount > x.Paid)
            .OrderBy(x => x.Bill.Year).ThenBy(x => x.Bill.Month).ToListAsync();
        return list.Select(x => (x.Bill, x.Paid)).ToList();
    }
}
