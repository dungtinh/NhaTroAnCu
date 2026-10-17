using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

/// <summary>Màu ô phòng trên sơ đồ.</summary>
public static class RoomColor
{
    public const string Empty = "empty";         // Trống
    public const string NoBill = "nobill";       // Đang thuê, chưa lập phiếu kỳ này
    public const string Unpaid = "unpaid";       // Còn nợ
    public const string Paid = "paid";           // Đã thu đủ
    public const string Maintenance = "maint";   // Đang sửa
}

public record RoomCard(int Id, string Code, int BlockId, string BlockCode, int Floor, int Number, string Status, decimal Rent, string Color,
    int? LeaseId, string? RepresentativeName, int PersonCount, decimal Outstanding, DateTime? StartDate, bool FurnitureDiffers);

public record PublicRoom(string Code, string BlockName, int Floor, decimal Rent, int MaxOccupants, List<string> Furniture);

public record RoomMap(List<RoomCard> Rooms, int BillingMonth, int BillingYear, int Total, int Occupied, int Empty, decimal Outstanding);

public record FurnitureLine(int ItemId, string Name, int Quantity, int DefaultQuantity)
{
    public bool Differs => Quantity != DefaultQuantity;
}

public class RoomDetails
{
    public required Room Room { get; init; }
    public List<FurnitureLine> Furniture { get; init; } = [];
    public Lease? CurrentLease { get; init; }
    public List<Bill> Bills { get; init; } = [];
    public Dictionary<int, decimal> BillPaid { get; init; } = [];
    public List<Lease> PastLeases { get; init; } = [];
    public List<Repair> Repairs { get; init; } = [];
    public decimal Outstanding => Bills.Sum(b => Math.Max(0, b.TotalAmount - BillPaid.GetValueOrDefault(b.Id)));
}

public class RoomService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Kỳ thu hiện tại: từ ngày thu (mồng 10) tháng này; trước ngày đó là kỳ của tháng trước.</summary>
    public static (int Month, int Year) CurrentBillingPeriod(int billingDay, DateTime? today = null)
    {
        var d = (today ?? DateTime.Today).Date;
        var m = d.Day >= billingDay ? d : d.AddMonths(-1);
        return (m.Month, m.Year);
    }

    /// <summary>Lượt thuê cần phiếu tháng M nếu nhận phòng trước ngày 1 của tháng M (kỳ đầu đã thu tới ngày thu tháng sau).</summary>
    public static bool NeedsMonthlyBill(Lease lease, int month, int year) => lease.StartDate < new DateTime(year, month, 1);

    public async Task<RoomMap> GetMapAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.Settings.AsNoTracking().FirstAsync();
        var (bm, by) = CurrentBillingPeriod(settings.BillingDay);

        var rooms = await db.Rooms.AsNoTracking().Include(r => r.Block).ThenInclude(b => b.DefaultFurniture).Include(r => r.Furniture)
            .OrderBy(r => r.Block.Code).ThenBy(r => r.Floor).ThenBy(r => r.Number).AsSplitQuery().ToListAsync();
        var leases = await db.Leases.AsNoTracking().Where(l => l.Status == LeaseStatus.Active)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant).AsSplitQuery().ToListAsync();
        var leaseIds = leases.Select(l => l.Id).ToList();
        var bills = await db.Bills.AsNoTracking().Where(b => leaseIds.Contains(b.LeaseId))
            .Select(b => new { b.Id, b.LeaseId, b.Month, b.Year, b.Kind, b.TotalAmount, Paid = b.Payments.Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();

        var cards = rooms.Select(r =>
        {
            var lease = leases.FirstOrDefault(l => l.RoomId == r.Id);
            var defaults = r.Block.DefaultFurniture.ToDictionary(d => d.FurnitureItemId, d => d.Quantity);
            var actual = r.Furniture.ToDictionary(f => f.FurnitureItemId, f => f.Quantity);
            bool differs = defaults.Keys.Union(actual.Keys).Any(k => defaults.GetValueOrDefault(k) != actual.GetValueOrDefault(k));

            if (lease == null)
                return new RoomCard(r.Id, r.Code, r.BlockId, r.Block.Code, r.Floor, r.Number, r.Status, r.Rent,
                    r.Status == RoomStatus.Maintenance ? RoomColor.Maintenance : RoomColor.Empty, null, null, 0, 0, null, differs);

            var lb = bills.Where(b => b.LeaseId == lease.Id).ToList();
            decimal outstanding = lb.Sum(b => Math.Max(0, b.TotalAmount - b.Paid));
            bool hasCurrent = !NeedsMonthlyBill(lease, bm, by) || lb.Any(b => b.Kind == BillKind.Monthly && b.Month == bm && b.Year == by);
            var color = outstanding > 0 ? RoomColor.Unpaid : hasCurrent ? RoomColor.Paid : RoomColor.NoBill;
            var active = lease.Tenants.Where(t => t.MoveOutDate == null).ToList();
            var rep = active.FirstOrDefault(t => t.IsRepresentative) ?? active.FirstOrDefault();
            return new RoomCard(r.Id, r.Code, r.BlockId, r.Block.Code, r.Floor, r.Number, r.Status, lease.MonthlyRent, color, lease.Id,
                rep?.Tenant.FullName, active.Count, outstanding, lease.StartDate, differs);
        }).ToList();

        return new RoomMap(cards, bm, by, cards.Count, cards.Count(c => c.LeaseId != null), cards.Count(c => c.LeaseId == null && c.Status != RoomStatus.Maintenance),
            cards.Sum(c => c.Outstanding));
    }

    public async Task<RoomDetails?> GetDetailsAsync(string code)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.AsNoTracking().Include(r => r.Block).ThenInclude(b => b.DefaultFurniture)
            .Include(r => r.Furniture).FirstOrDefaultAsync(r => r.Code == code);
        if (room == null) return null;

        var items = await db.FurnitureItems.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync();
        var furniture = BuildFurnitureLines(items, room.Furniture.ToDictionary(f => f.FurnitureItemId, f => f.Quantity),
            room.Block.DefaultFurniture.ToDictionary(d => d.FurnitureItemId, d => d.Quantity));

        var leases = await db.Leases.AsNoTracking().Where(l => l.RoomId == room.Id)
            .Include(l => l.Tenants).ThenInclude(t => t.Tenant).Include(l => l.Penalties)
            .OrderByDescending(l => l.StartDate).AsSplitQuery().ToListAsync();
        var current = leases.FirstOrDefault(l => l.Status == LeaseStatus.Active);

        List<Bill> bills = [];
        Dictionary<int, decimal> paid = [];
        if (current != null)
        {
            bills = await db.Bills.AsNoTracking().Where(b => b.LeaseId == current.Id).OrderByDescending(b => b.Year).ThenByDescending(b => b.Month).ThenByDescending(b => b.Id).ToListAsync();
            var ids = bills.Select(b => b.Id).ToList();
            paid = await db.Transactions.Where(t => t.BillId != null && ids.Contains(t.BillId.Value)).GroupBy(t => t.BillId!.Value)
                .Select(g => new { g.Key, Sum = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        }

        var repairs = await db.Repairs.AsNoTracking().Include(r => r.FurnitureItem).Where(r => r.RoomId == room.Id).OrderByDescending(r => r.Date).Take(30).ToListAsync();

        return new RoomDetails
        {
            Room = room, Furniture = furniture, CurrentLease = current, Bills = bills, BillPaid = paid,
            PastLeases = leases.Where(l => l.Status != LeaseStatus.Active).ToList(), Repairs = repairs
        };
    }

    public static List<FurnitureLine> BuildFurnitureLines(List<FurnitureItem> items, Dictionary<int, int> actual, Dictionary<int, int> defaults) =>
        items.Where(i => i.IsActive || actual.ContainsKey(i.Id) || defaults.ContainsKey(i.Id))
             .Select(i => new FurnitureLine(i.Id, i.Name, actual.GetValueOrDefault(i.Id), defaults.GetValueOrDefault(i.Id))).ToList();

    /// <summary>Phòng trống kèm đồ đạc, dùng cho trang giới thiệu công khai.</summary>
    public async Task<List<PublicRoom>> ListVacantForAdAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.Status == RoomStatus.Empty)
            .OrderBy(r => r.Block.Code).ThenBy(r => r.Floor).ThenBy(r => r.Number)
            .Select(r => new { r.Code, Block = r.Block.Name, r.Floor, r.Rent, r.MaxOccupants,
                Furniture = r.Furniture.OrderBy(f => f.FurnitureItem.SortOrder).Select(f => f.Quantity > 1 ? f.Quantity + " " + f.FurnitureItem.Name : f.FurnitureItem.Name).ToList() })
            .ToListAsync();
        return rooms.Select(r => new PublicRoom(r.Code, r.Block, r.Floor, r.Rent, r.MaxOccupants, r.Furniture)).ToList();
    }

    public async Task<List<Room>> ListRoomsAsync(string? status = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Rooms.AsNoTracking().Include(r => r.Block).AsQueryable();
        if (status != null) q = q.Where(r => r.Status == status);
        return await q.OrderBy(r => r.Block.Code).ThenBy(r => r.Floor).ThenBy(r => r.Number).ToListAsync();
    }

    public async Task UpdateRoomAsync(int roomId, decimal rent, int meterReading, int maxOccupants, string? note, bool maintenance, Dictionary<int, int> furniture)
    {
        if (rent < 0 || meterReading < 0) throw new InvalidOperationException("Giá thuê và số điện không được âm.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.Include(r => r.Furniture).FirstAsync(r => r.Id == roomId);
        room.Rent = rent;
        room.MeterReading = meterReading;
        room.MaxOccupants = Math.Clamp(maxOccupants, 1, 20);
        room.Note = note;
        if (room.Status != RoomStatus.Occupied) room.Status = maintenance ? RoomStatus.Maintenance : RoomStatus.Empty;
        SetupService.SyncFurniture(room.Furniture, furniture, (itemId, qty) => new RoomFurniture { RoomId = roomId, FurnitureItemId = itemId, Quantity = qty },
            x => x.FurnitureItemId, (x, q) => x.Quantity = q, db);
        await db.SaveChangesAsync();
    }
}
