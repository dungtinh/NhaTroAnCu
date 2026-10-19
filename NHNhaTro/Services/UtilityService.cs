using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class UtilityBillForm
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int? ElectricityKwh { get; set; }
    public decimal ElectricityAmount { get; set; }
    public int? WaterM3 { get; set; }
    public decimal WaterAmount { get; set; }
    public DateTime PaidDate { get; set; } = DateTime.Today;
    public int AccountId { get; set; }
    public string? Note { get; set; }
    public List<PendingFile> Receipts { get; set; } = [];
}

/// <summary>Hóa đơn điện, nước tổng của cả nhà trọ mỗi kỳ: lưu thì ghi dòng chi vào sổ thu chi.</summary>
public class UtilityService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files)
{
    public async Task<UtilityBill?> GetAsync(int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UtilityBills.AsNoTracking().Include(u => u.Account).FirstOrDefaultAsync(u => u.Month == month && u.Year == year);
    }

    public async Task<int> SaveAsync(UtilityBillForm f, string? user)
    {
        if (f.ElectricityAmount < 0 || f.WaterAmount < 0 || f.ElectricityKwh < 0 || f.WaterM3 < 0) throw new InvalidOperationException("Số liệu không được âm.");
        if (f.ElectricityAmount == 0 && f.WaterAmount == 0) throw new InvalidOperationException("Nhập tiền điện hoặc tiền nước tổng.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Accounts.AnyAsync(a => a.Id == f.AccountId)) throw new InvalidOperationException("Chọn tài khoản chi.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var u = await db.UtilityBills.FirstOrDefaultAsync(x => x.Month == f.Month && x.Year == f.Year);
        if (u == null)
        {
            u = new UtilityBill { Month = f.Month, Year = f.Year, CreatedAt = DateTime.Now, CreatedBy = user };
            db.UtilityBills.Add(u);
        }
        u.ElectricityKwh = f.ElectricityKwh;
        u.ElectricityAmount = f.ElectricityAmount;
        u.WaterM3 = f.WaterM3;
        u.WaterAmount = f.WaterAmount;
        u.PaidDate = f.PaidDate.Date;
        u.AccountId = f.AccountId;
        u.Note = string.IsNullOrWhiteSpace(f.Note) ? null : f.Note.Trim();
        await db.SaveChangesAsync();

        // Ghi lại dòng chi: bỏ dòng cũ của kỳ này, tạo dòng mới theo số vừa lưu
        db.Transactions.RemoveRange(db.Transactions.Where(t => t.UtilityBillId == u.Id));
        string period = $"{f.Month:00}/{f.Year}";
        if (u.ElectricityAmount > 0)
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = u.AccountId, Date = u.PaidDate, Amount = -u.ElectricityAmount, Category = TxCategory.Electricity, UtilityBillId = u.Id,
                Description = $"Tiền điện tổng kỳ {period}" + (u.ElectricityKwh is int k ? $" ({k} số)" : ""), CreatedAt = DateTime.Now, CreatedBy = user
            });
        if (u.WaterAmount > 0)
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = u.AccountId, Date = u.PaidDate, Amount = -u.WaterAmount, Category = TxCategory.Water, UtilityBillId = u.Id,
                Description = $"Tiền nước tổng kỳ {period}" + (u.WaterM3 is int m ? $" ({m} m³)" : ""), CreatedAt = DateTime.Now, CreatedBy = user
            });
        AttachmentService.Attach(db, AttachmentOwner.Utility, u.Id, f.Receipts, user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return u.Id;
    }

    /// <summary>Xóa hóa đơn tổng của kỳ và các dòng chi của nó (chỉ Quản trị).</summary>
    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var u = await db.UtilityBills.FindAsync(id) ?? throw new InvalidOperationException("Không tìm thấy hóa đơn.");
        var atts = await db.Attachments.Where(a => a.EntityType == AttachmentOwner.Utility && a.EntityId == id).ToListAsync();
        db.Transactions.RemoveRange(db.Transactions.Where(t => t.UtilityBillId == id));
        db.Attachments.RemoveRange(atts);
        db.UtilityBills.Remove(u);
        await db.SaveChangesAsync();
        foreach (var a in atts) files.Delete(a.Path);
    }
}
