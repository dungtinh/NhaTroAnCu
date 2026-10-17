using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class RepairForm
{
    public DateTime Date { get; set; } = DateTime.Today;
    /// <summary>Null = khu vực chung.</summary>
    public int? RoomId { get; set; }
    public string? Location { get; set; }
    public int? FurnitureItemId { get; set; }
    public string Description { get; set; } = "";
    public string? PerformedBy { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public List<PendingFile> BeforePhotos { get; set; } = [];
    public List<PendingFile> AfterPhotos { get; set; } = [];
    public List<PendingFile> Receipts { get; set; } = [];
}

public class PurchaseForm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Description { get; set; } = "";
    public string? Supplier { get; set; }
    public int? RoomId { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public bool IsAdvertising { get; set; }
    public List<PendingFile> Receipts { get; set; } = [];
}

public record RepairRow(int Id, DateTime Date, string Place, string? Item, string Description, string? PerformedBy, decimal Amount, string AccountName,
    string? CreatedBy, DateTime CreatedAt, int PhotoCount, int ReceiptCount, int RepeatCount);

public record PurchaseRow(int Id, DateTime Date, string Description, string? Supplier, string? RoomCode, decimal Amount, string AccountName, bool IsAdvertising,
    string? CreatedBy, int ReceiptCount);

public class ExpenseService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files)
{
    public async Task<int> CreateRepairAsync(RepairForm f, string? user)
    {
        if (string.IsNullOrWhiteSpace(f.Description)) throw new InvalidOperationException("Nhập nội dung sửa chữa.");
        if (f.Amount < 0) throw new InvalidOperationException("Số tiền không được âm.");
        if (f.RoomId == null && string.IsNullOrWhiteSpace(f.Location)) throw new InvalidOperationException("Chọn phòng hoặc ghi vị trí khu vực chung.");
        if (f.BeforePhotos.Count + f.AfterPhotos.Count == 0) throw new InvalidOperationException("Bắt buộc chụp ảnh hư hỏng hoặc ảnh sau khi sửa.");
        if (f.Amount > 0 && f.Receipts.Count == 0) throw new InvalidOperationException("Bắt buộc tải chứng từ chuyển khoản/hóa đơn khi có chi tiền.");

        await using var db = await dbFactory.CreateDbContextAsync();
        if (f.AccountId == 0) f.AccountId = (await db.Settings.AsNoTracking().FirstAsync()).PersonalAccountId;
        await using var tx = await db.Database.BeginTransactionAsync();
        var repair = new Repair
        {
            Date = f.Date.Date, RoomId = f.RoomId, Location = f.Location?.Trim(), FurnitureItemId = f.FurnitureItemId, Description = f.Description.Trim(),
            PerformedBy = f.PerformedBy?.Trim(), Amount = f.Amount, AccountId = f.AccountId, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Repairs.Add(repair);
        await db.SaveChangesAsync();

        if (f.Amount > 0)
        {
            var room = f.RoomId is int rid ? await db.Rooms.AsNoTracking().FirstAsync(r => r.Id == rid) : null;
            db.Transactions.Add(new MoneyTransaction
            {
                AccountId = f.AccountId, Date = repair.Date, Amount = -f.Amount, Category = TxCategory.Repair,
                Description = $"Sửa chữa {(room != null ? "phòng " + room.Code : f.Location)}: {repair.Description}".Truncate(500),
                RoomId = f.RoomId, RepairId = repair.Id, CreatedAt = DateTime.Now, CreatedBy = user
            });
        }
        AttachmentService.Attach(db, AttachmentOwner.Repair, repair.Id, f.BeforePhotos.Select(p => p with { Kind = AttachmentKind.RepairBefore }), user);
        AttachmentService.Attach(db, AttachmentOwner.Repair, repair.Id, f.AfterPhotos.Select(p => p with { Kind = AttachmentKind.RepairAfter }), user);
        AttachmentService.Attach(db, AttachmentOwner.Repair, repair.Id, f.Receipts.Select(p => p with { Kind = AttachmentKind.RepairReceipt }), user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return repair.Id;
    }

    public async Task<List<RepairRow>> ListRepairsAsync(DateTime? from, DateTime? to, int? roomId, bool commonOnly, string? search)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Repairs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(r => r.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(r => r.Date <= to.Value.Date);
        if (roomId.HasValue) q = q.Where(r => r.RoomId == roomId);
        if (commonOnly) q = q.Where(r => r.RoomId == null);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(r => r.Description.Contains(search) || (r.PerformedBy != null && r.PerformedBy.Contains(search)) || (r.Location != null && r.Location.Contains(search)));

        var rows = await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).Take(500)
            .Select(r => new
            {
                r.Id, r.Date, RoomCode = r.Room != null ? r.Room.Code : null, r.RoomId, r.Location, Item = r.FurnitureItem != null ? r.FurnitureItem.Name : null,
                r.FurnitureItemId, r.Description, r.PerformedBy, r.Amount, Account = r.Account.Name, r.CreatedBy, r.CreatedAt,
                Photos = db.Attachments.Count(a => a.EntityType == AttachmentOwner.Repair && a.EntityId == r.Id && a.Kind != AttachmentKind.RepairReceipt),
                Receipts = db.Attachments.Count(a => a.EntityType == AttachmentOwner.Repair && a.EntityId == r.Id && a.Kind == AttachmentKind.RepairReceipt)
            }).ToListAsync();

        // Đếm số lần sửa cùng phòng + cùng đồ đạc trong 180 ngày (cảnh báo sửa lặp lại)
        var since = DateTime.Today.AddDays(-180);
        var recent = await db.Repairs.AsNoTracking().Where(r => r.Date >= since && r.RoomId != null)
            .GroupBy(r => new { r.RoomId, r.FurnitureItemId }).Select(g => new { g.Key.RoomId, g.Key.FurnitureItemId, Count = g.Count() }).ToListAsync();

        return rows.Select(r => new RepairRow(r.Id, r.Date, r.RoomCode != null ? "Phòng " + r.RoomCode : "Khu chung: " + r.Location, r.Item, r.Description,
            r.PerformedBy, r.Amount, r.Account, r.CreatedBy, r.CreatedAt, r.Photos, r.Receipts,
            r.RoomId == null ? 0 : recent.FirstOrDefault(x => x.RoomId == r.RoomId && x.FurnitureItemId == r.FurnitureItemId)?.Count ?? 0)).ToList();
    }

    public async Task<Repair?> GetRepairAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Repairs.AsNoTracking().Include(r => r.Room).Include(r => r.FurnitureItem).Include(r => r.Account).FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task DeleteRepairAsync(int id) => await DeleteAsync(AttachmentOwner.Repair, id, db => db.Repairs.FindAsync(id).AsTask(), t => t.RepairId == id);

    public async Task<int> CreatePurchaseAsync(PurchaseForm f, string? user)
    {
        if (string.IsNullOrWhiteSpace(f.Description)) throw new InvalidOperationException("Nhập nội dung mua sắm.");
        if (f.Amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        if (f.Receipts.Count == 0) throw new InvalidOperationException("Bắt buộc tải ảnh chứng từ (hóa đơn, chuyển khoản).");

        await using var db = await dbFactory.CreateDbContextAsync();
        if (f.AccountId == 0) f.AccountId = (await db.Settings.AsNoTracking().FirstAsync()).PersonalAccountId;
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = new Purchase
        {
            Date = f.Date.Date, Description = f.Description.Trim(), Supplier = f.Supplier?.Trim(), RoomId = f.RoomId, Amount = f.Amount,
            AccountId = f.AccountId, IsAdvertising = f.IsAdvertising, CreatedAt = DateTime.Now, CreatedBy = user
        };
        db.Purchases.Add(p);
        await db.SaveChangesAsync();
        db.Transactions.Add(new MoneyTransaction
        {
            AccountId = f.AccountId, Date = p.Date, Amount = -f.Amount, Category = f.IsAdvertising ? TxCategory.Advertising : TxCategory.Purchase,
            Description = ((f.IsAdvertising ? "Quảng cáo: " : "Mua sắm: ") + p.Description).Truncate(500), RoomId = f.RoomId, PurchaseId = p.Id,
            CreatedAt = DateTime.Now, CreatedBy = user
        });
        AttachmentService.Attach(db, AttachmentOwner.Purchase, p.Id, f.Receipts.Select(r => r with { Kind = AttachmentKind.PurchaseReceipt }), user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return p.Id;
    }

    public async Task<List<PurchaseRow>> ListPurchasesAsync(DateTime? from, DateTime? to, bool? advertising, string? search)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Purchases.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(r => r.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(r => r.Date <= to.Value.Date);
        if (advertising.HasValue) q = q.Where(r => r.IsAdvertising == advertising.Value);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(r => r.Description.Contains(search) || (r.Supplier != null && r.Supplier.Contains(search)));
        return await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id).Take(500)
            .Select(r => new PurchaseRow(r.Id, r.Date, r.Description, r.Supplier, r.Room != null ? r.Room.Code : null, r.Amount, r.Account.Name, r.IsAdvertising,
                r.CreatedBy, db.Attachments.Count(a => a.EntityType == AttachmentOwner.Purchase && a.EntityId == r.Id)))
            .ToListAsync();
    }

    public async Task DeletePurchaseAsync(int id) => await DeleteAsync(AttachmentOwner.Purchase, id, db => db.Purchases.FindAsync(id).AsTask(), t => t.PurchaseId == id);

    private async Task DeleteAsync<T>(string owner, int id, Func<ApplicationDbContext, Task<T?>> find, System.Linq.Expressions.Expression<Func<MoneyTransaction, bool>> txFilter) where T : class
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = await find(db) ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
        var txs = await db.Transactions.Where(txFilter).ToListAsync();
        var atts = await db.Attachments.Where(a => a.EntityType == owner && a.EntityId == id).ToListAsync();
        db.Transactions.RemoveRange(txs);
        db.Attachments.RemoveRange(atts);
        db.Remove(entity);
        await db.SaveChangesAsync();
        foreach (var a in atts) files.Delete(a.Path);
    }
}

public static class StringExtensions
{
    public static string Truncate(this string s, int max) => s.Length <= max ? s : s[..max];
}
