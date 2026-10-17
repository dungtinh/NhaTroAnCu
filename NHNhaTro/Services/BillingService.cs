using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public class BillForm
{
    public int? BillId { get; set; }
    public int RoomId { get; set; }
    public int ContractId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int WaterIndexStart { get; set; }
    public int WaterIndexEnd { get; set; }
    public decimal WaterPrice { get; set; }
    public decimal ElectricityAmount { get; set; }
    public decimal RentAmount { get; set; }
    public decimal ExtraCharge { get; set; }
    public decimal Discount { get; set; }
    public string? BillNote { get; set; }

    public int WaterUsed => Math.Max(0, WaterIndexEnd - WaterIndexStart);
    public decimal WaterAmount => WaterUsed * WaterPrice;
    public decimal TotalAmount => Math.Max(0, ElectricityAmount + WaterAmount + RentAmount + ExtraCharge - Discount);
}

public class BillingService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Chỉ số nước cuối kỳ lớn nhất đã ghi của phòng (dùng làm chỉ số đầu kỳ cho phiếu mới).</summary>
    public static async Task<int> GetHighestWaterIndexEndAsync(ApplicationDbContext db, int roomId, int? exceptBillId = null) =>
        await db.UtilityBills.Where(b => b.RoomId == roomId && b.Id != (exceptBillId ?? 0)).MaxAsync(b => (int?)b.WaterIndexEnd) ?? 0;

    /// <summary>Lấy form phiếu báo của phòng cho tháng/năm: nạp phiếu đã có hoặc khởi tạo phiếu mới.</summary>
    public async Task<BillForm?> GetFormAsync(int roomId, int month, int year)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return null;

        var contract = await RoomService.CurrentContractsOfRoom(db, roomId).AsNoTracking()
            .Include(c => c.ContractRooms)
            .FirstOrDefaultAsync();
        if (contract == null) return null;

        var bill = await db.UtilityBills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.ContractId == contract.Id && b.RoomId == roomId && b.Month == month && b.Year == year);

        if (bill != null)
        {
            return new BillForm
            {
                BillId = bill.Id,
                RoomId = roomId,
                ContractId = contract.Id,
                Month = bill.Month,
                Year = bill.Year,
                WaterIndexStart = bill.WaterIndexStart,
                WaterIndexEnd = bill.WaterIndexEnd,
                WaterPrice = contract.WaterPrice,
                ElectricityAmount = bill.ElectricityAmount,
                RentAmount = bill.RentAmount,
                ExtraCharge = bill.ExtraCharge,
                Discount = bill.Discount,
                BillNote = bill.BillNote
            };
        }

        int waterPrev = await GetHighestWaterIndexEndAsync(db, roomId);
        var form = new BillForm
        {
            RoomId = roomId,
            ContractId = contract.Id,
            Month = month,
            Year = year,
            WaterIndexStart = waterPrev,
            WaterIndexEnd = waterPrev,
            WaterPrice = contract.WaterPrice,
            RentAmount = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == roomId)?.PriceAgreed ?? room.DefaultPrice,
            BillNote = ""
        };
        ApplyFirstMonthAdjustment(form, contract);
        return form;
    }

    /// <summary>
    /// Tháng đầu tiên: tiền phòng tính theo ngày từ ngày bắt đầu đến ngày chốt (ngày chuyển vào).
    /// Bắt đầu trước ngày chốt thì phụ thu, sau ngày chốt thì giảm trừ (giá ngày = giá tháng / 30).
    /// Bản cũ tính phụ thu = giá ngày × (ngày chốt − ngày chốt) nên luôn bằng 0; đã sửa thành số ngày chênh lệch.
    /// </summary>
    public static void ApplyFirstMonthAdjustment(BillForm form, Contract contract)
    {
        if (form.Month != contract.MoveInDate.Month || form.Year != contract.MoveInDate.Year) return;
        var contractRoom = contract.ContractRooms.FirstOrDefault(cr => cr.RoomId == form.RoomId);
        if (contractRoom == null) return;

        int moveInDay = contract.MoveInDate.Day;
        int startDay = contract.StartDate.Day;
        decimal pricePerDay = Math.Round(contractRoom.PriceAgreed / 30, 0);

        if (startDay < moveInDay)
        {
            form.ExtraCharge = pricePerDay * (moveInDay - startDay);
            form.Discount = 0;
            form.BillNote = $"Phụ thu {moveInDay - startDay} ngày (bắt đầu ngày {startDay:00}/{contract.StartDate.Month:00})";
        }
        else if (startDay > moveInDay)
        {
            form.ExtraCharge = 0;
            form.Discount = pricePerDay * (startDay - moveInDay);
            form.BillNote = $"Giảm trừ {startDay - moveInDay} ngày (bắt đầu ngày {startDay:00}/{contract.StartDate.Month:00})";
        }
        else
        {
            form.ExtraCharge = 0;
            form.Discount = 0;
            form.BillNote = $"Chuyển vào ngày {moveInDay:00}/{form.Month:00}";
        }
    }

    public async Task<(bool IsNew, int BillId)> SaveAsync(BillForm form)
    {
        if (form.WaterIndexEnd < form.WaterIndexStart)
            throw new InvalidOperationException("Chỉ số nước tháng này không được nhỏ hơn chỉ số tháng trước.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == form.ContractId)
            ?? throw new InvalidOperationException("Không tìm thấy hợp đồng.");

        var bill = await db.UtilityBills.FirstOrDefaultAsync(b =>
            b.ContractId == form.ContractId && b.RoomId == form.RoomId && b.Month == form.Month && b.Year == form.Year);
        bool isNew = bill == null;
        if (bill == null)
        {
            bill = new UtilityBill { CreatedAt = DateTime.Now };
            db.UtilityBills.Add(bill);
        }

        bill.Month = form.Month;
        bill.Year = form.Year;
        bill.RoomId = form.RoomId;
        bill.ContractId = form.ContractId;
        bill.WaterIndexStart = form.WaterIndexStart;
        bill.WaterIndexEnd = form.WaterIndexEnd;
        bill.ElectricityAmount = form.ElectricityAmount;
        bill.WaterAmount = form.WaterUsed * contract.WaterPrice;
        bill.RentAmount = form.RentAmount;
        bill.ExtraCharge = form.ExtraCharge;
        bill.Discount = form.Discount;
        bill.TotalAmount = bill.ElectricityAmount + bill.WaterAmount + bill.RentAmount + bill.ExtraCharge - bill.Discount;
        bill.BillNote = form.BillNote;
        bill.BillStatus = "Final";
        bill.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        return (isNew, bill.Id);
    }

    public async Task DeleteAsync(int billId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var bill = await db.UtilityBills.FindAsync(billId) ?? throw new InvalidOperationException("Không tìm thấy phiếu báo tiền.");
        db.UtilityBills.Remove(bill);
        await db.SaveChangesAsync();
    }

    public static async Task<IncomeExpenseCategory> GetOrCreateSystemCategoryAsync(ApplicationDbContext db, string name, string type)
    {
        var category = await db.IncomeExpenseCategories.FirstOrDefaultAsync(c => c.Name == name && c.Type == type);
        if (category != null) return category;
        category = new IncomeExpenseCategory { Name = name, Type = type, IsSystem = true, IsActive = true, CreatedAt = DateTime.Now };
        db.IncomeExpenseCategories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    /// <summary>Ghi nhận thu tiền phòng, trả về Id giao dịch để in hóa đơn.</summary>
    public async Task<int> RecordRoomPaymentAsync(int contractId, decimal amount, DateTime paymentDate, string? note, string? userId)
    {
        if (amount <= 0) throw new InvalidOperationException("Số tiền thu phải lớn hơn 0.");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Contracts.AnyAsync(c => c.Id == contractId))
            throw new InvalidOperationException("Không tìm thấy hợp đồng.");

        var category = await GetOrCreateSystemCategoryAsync(db, SystemCategories.RoomPayment, CategoryType.Income);
        var income = new IncomeExpense
        {
            CategoryId = category.Id,
            ContractId = contractId,
            Amount = amount,
            TransactionDate = paymentDate.Date,
            Description = string.IsNullOrWhiteSpace(note) ? "Thu tiền phòng" : $"Thu tiền phòng - {note.Trim()}",
            ReferenceNumber = $"THU-{DateTime.Now:yyyyMMddHHmmss}",
            CreatedBy = userId,
            CreatedAt = DateTime.Now
        };
        db.IncomeExpenses.Add(income);
        await db.SaveChangesAsync();
        return income.Id;
    }
}
