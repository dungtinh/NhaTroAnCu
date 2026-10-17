namespace NHNhaTro.Data.Entities;

// ============================ CẤU HÌNH ============================

/// <summary>Cấu hình chung (một dòng duy nhất, Id = 1).</summary>
public class AppSetting
{
    public int Id { get; set; } = 1;
    /// <summary>Giá điện mỗi số (kWh).</summary>
    public decimal ElectricityPrice { get; set; } = 3500;
    /// <summary>Tiền nước mỗi người mỗi tháng.</summary>
    public decimal WaterPricePerPerson { get; set; } = 80000;
    /// <summary>Ngày thu tiền hằng tháng.</summary>
    public int BillingDay { get; set; } = 10;
    public int PersonalAccountId { get; set; }
    public int BusinessAccountId { get; set; }

    // Thông tin nhà trọ (chân trang, chứng từ, trang quảng cáo)
    public string Name { get; set; } = "Nhà trọ An Cư";
    public string? Address { get; set; }
    public string? Owner { get; set; }
    public string? Phone { get; set; }
    public string? RegisterArea { get; set; }

    // Quảng cáo
    public string? AdTitle { get; set; }
    public string? AdDescription { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? MapUrl { get; set; }
    public string? ZaloPhone { get; set; }
}

public class Block
{
    public int Id { get; set; }
    /// <summary>A, B...</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Floors { get; set; } = 4;
    public int RoomsPerFloor { get; set; } = 8;
    public decimal DefaultRent { get; set; }
    public List<Room> Rooms { get; set; } = [];
    public List<BlockFurniture> DefaultFurniture { get; set; } = [];
}

public class FurnitureItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Đồ đạc mặc định của một dãy.</summary>
public class BlockFurniture
{
    public int Id { get; set; }
    public int BlockId { get; set; }
    public int FurnitureItemId { get; set; }
    public int Quantity { get; set; }
    public Block Block { get; set; } = null!;
    public FurnitureItem FurnitureItem { get; set; } = null!;
}

// ============================ PHÒNG ============================

public static class RoomStatus
{
    public const string Empty = "Empty";
    public const string Occupied = "Occupied";
    public const string Maintenance = "Maintenance";

    public static string Display(string? s) => s switch
    {
        Occupied => "Đang thuê",
        Maintenance => "Đang sửa",
        _ => "Trống"
    };
}

public class Room
{
    public int Id { get; set; }
    public int BlockId { get; set; }
    /// <summary>Tầng 0 = tầng trệt.</summary>
    public int Floor { get; set; }
    public int Number { get; set; }
    /// <summary>A0.1, B3.8...</summary>
    public string Code { get; set; } = "";
    public decimal Rent { get; set; }
    /// <summary>Chỉ số công tơ điện hiện tại (số chốt gần nhất).</summary>
    public int MeterReading { get; set; }
    public string Status { get; set; } = RoomStatus.Empty;
    public int MaxOccupants { get; set; } = 4;
    public string? Note { get; set; }

    public Block Block { get; set; } = null!;
    public List<RoomFurniture> Furniture { get; set; } = [];
    public List<Lease> Leases { get; set; } = [];
}

/// <summary>Đồ đạc thực có trong phòng.</summary>
public class RoomFurniture
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int FurnitureItemId { get; set; }
    public int Quantity { get; set; }
    public Room Room { get; set; } = null!;
    public FurnitureItem FurnitureItem { get; set; } = null!;
}

// ============================ KHÁCH THUÊ ============================

public static class LeaseStatus
{
    public const string Active = "Active";
    public const string Ended = "Ended";
}

/// <summary>Một lượt thuê phòng (từ lúc nhận phòng đến lúc trả phòng).</summary>
public class Lease
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal Deposit { get; set; }
    public int StartMeterReading { get; set; }
    public int? EndMeterReading { get; set; }
    public string Status { get; set; } = LeaseStatus.Active;
    public string? ContractScanPath { get; set; }
    public string? Note { get; set; }

    // Trả phòng
    public decimal? CheckoutElectricity { get; set; }
    public decimal? CheckoutPenalty { get; set; }
    public decimal? CheckoutOther { get; set; }
    public decimal? DepositRefunded { get; set; }
    public decimal? CheckoutCollected { get; set; }
    public string? CheckoutNote { get; set; }
    public string? FurnitureWarnings { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Room Room { get; set; } = null!;
    public List<LeaseTenant> Tenants { get; set; } = [];
    public List<Bill> Bills { get; set; } = [];
    public List<LeasePenalty> Penalties { get; set; } = [];
}

/// <summary>Hồ sơ một người (theo số CCCD/hộ chiếu), dùng lại qua nhiều lượt thuê.</summary>
public class Tenant
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string IdentityCard { get; set; } = "";
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? PermanentAddress { get; set; }
    public string? Ethnicity { get; set; }
    public string? Nationality { get; set; }
    public string? VehiclePlate { get; set; }
    /// <summary>Ảnh CCCD, nhiều ảnh cách nhau bằng dấu ;</summary>
    public string? Photos { get; set; }

    public List<LeaseTenant> Leases { get; set; } = [];

    public IEnumerable<string> PhotoList =>
        string.IsNullOrWhiteSpace(Photos) ? [] : Photos.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class LeaseTenant
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public int TenantId { get; set; }
    public bool IsRepresentative { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }

    public Lease Lease { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}

/// <summary>Khoản phạt khi trả phòng (bẩn tường, vỡ kính...).</summary>
public class LeasePenalty
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public Lease Lease { get; set; } = null!;
}

// ============================ PHIẾU THU ============================

public static class BillKind
{
    /// <summary>Kỳ đầu khi nhận phòng.</summary>
    public const string MoveIn = "MoveIn";
    /// <summary>Thu tiền hằng tháng.</summary>
    public const string Monthly = "Monthly";
    /// <summary>Quyết toán khi trả phòng.</summary>
    public const string MoveOut = "MoveOut";

    public static string Display(string? k) => k switch
    {
        MoveIn => "Kỳ đầu",
        MoveOut => "Trả phòng",
        _ => "Hằng tháng"
    };
}

public class Bill
{
    public int Id { get; set; }
    public int LeaseId { get; set; }
    public string Kind { get; set; } = BillKind.Monthly;
    /// <summary>Tháng/năm của kỳ thu (kỳ đầu: tháng nhận phòng).</summary>
    public int Month { get; set; }
    public int Year { get; set; }
    /// <summary>Tiền phòng tính cho khoảng [PeriodFrom, PeriodTo).</summary>
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal RentAmount { get; set; }
    public int? MeterStart { get; set; }
    public int? MeterEnd { get; set; }
    public decimal ElectricityAmount { get; set; }
    public int PersonCount { get; set; }
    public decimal WaterAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Lease Lease { get; set; } = null!;
    public List<MoneyTransaction> Payments { get; set; } = [];
}

// ============================ TÀI KHOẢN, THU CHI ============================

public static class AccountKind
{
    public const string Personal = "Personal";
    public const string Business = "Business";
}

public class MoneyAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = AccountKind.Business;
    public decimal OpeningBalance { get; set; }
    public DateTime OpeningDate { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolder { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class TxCategory
{
    public const string Rent = "Rent";
    public const string Electricity = "Electricity";
    public const string Water = "Water";
    public const string Deposit = "Deposit";
    public const string DepositRefund = "DepositRefund";
    public const string Penalty = "Penalty";
    public const string Repair = "Repair";
    public const string Purchase = "Purchase";
    public const string Advertising = "Advertising";
    public const string Transfer = "Transfer";
    public const string OtherIncome = "OtherIncome";
    public const string OtherExpense = "OtherExpense";
    /// <summary>Tiền thu theo phiếu (gộp phòng + điện + nước + khác).</summary>
    public const string BillPayment = "BillPayment";

    public static readonly string[] All = [BillPayment, Rent, Electricity, Water, Deposit, DepositRefund, Penalty, Repair, Purchase, Advertising, Transfer, OtherIncome, OtherExpense];

    public static string Display(string? c) => c switch
    {
        Rent => "Tiền phòng",
        Electricity => "Tiền điện",
        Water => "Tiền nước",
        Deposit => "Thu cọc",
        DepositRefund => "Trả cọc",
        Penalty => "Tiền phạt",
        Repair => "Sửa chữa",
        Purchase => "Mua sắm",
        Advertising => "Quảng cáo",
        Transfer => "Chuyển nội bộ",
        OtherIncome => "Thu khác",
        OtherExpense => "Chi khác",
        BillPayment => "Thu tiền phòng/điện/nước",
        _ => c ?? ""
    };
}

/// <summary>Một dòng thu (+) hoặc chi (−) trên một tài khoản.</summary>
public class MoneyTransaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Dương = thu vào, âm = chi ra.</summary>
    public decimal Amount { get; set; }
    public string Category { get; set; } = TxCategory.OtherIncome;
    public string? Description { get; set; }
    public int? RoomId { get; set; }
    public int? LeaseId { get; set; }
    public int? BillId { get; set; }
    public int? RepairId { get; set; }
    public int? PurchaseId { get; set; }
    /// <summary>Hai dòng của một lần chuyển nội bộ dùng chung mã này.</summary>
    public Guid? TransferGroup { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public MoneyAccount Account { get; set; } = null!;
    public Room? Room { get; set; }
    public Lease? Lease { get; set; }
    public Bill? Bill { get; set; }
    public Repair? Repair { get; set; }
    public Purchase? Purchase { get; set; }
}

// ============================ SỬA CHỮA, MUA SẮM ============================

public class Repair
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>Null = khu vực chung.</summary>
    public int? RoomId { get; set; }
    public string? Location { get; set; }
    public int? FurnitureItemId { get; set; }
    public string Description { get; set; } = "";
    public string? PerformedBy { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Room? Room { get; set; }
    public FurnitureItem? FurnitureItem { get; set; }
    public MoneyAccount Account { get; set; } = null!;
}

public class Purchase
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string? Supplier { get; set; }
    public int? RoomId { get; set; }
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public bool IsAdvertising { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public Room? Room { get; set; }
    public MoneyAccount Account { get; set; } = null!;
}

// ============================ FILE ĐÍNH KÈM ============================

public static class AttachmentKind
{
    public const string ContractScan = "ContractScan";
    public const string PaymentReceipt = "PaymentReceipt";
    public const string CheckoutPhoto = "CheckoutPhoto";
    public const string RepairBefore = "RepairBefore";
    public const string RepairAfter = "RepairAfter";
    public const string RepairReceipt = "RepairReceipt";
    public const string PurchaseReceipt = "PurchaseReceipt";
    public const string Gallery = "Gallery";
    public const string RoomPhoto = "RoomPhoto";
}

/// <summary>Ảnh/chứng từ gắn với một đối tượng (EntityType + EntityId).</summary>
public class Attachment
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string? FileName { get; set; }
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public bool IsImage => System.IO.Path.GetExtension(Path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
}

public static class AttachmentOwner
{
    public const string Lease = "Lease";
    public const string Transaction = "Transaction";
    public const string Repair = "Repair";
    public const string Purchase = "Purchase";
    public const string Site = "Site";
    public const string Room = "Room";
}

/// <summary>Cấu hình API key FPT.AI để quét CCCD (bảng FPTReaderAPI).</summary>
public class FptReaderApi
{
    public string ApiKey { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Status { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
}
