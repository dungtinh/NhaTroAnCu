namespace NHNhaTro.Data.Entities;

// Các entity ánh xạ đúng tên bảng/cột của database NhaTroAnCu (bản MVC 5 cũ)
// để bản .NET 10 dùng lại được dữ liệu hiện có. Cấu hình kiểu cột nằm trong ApplicationDbContext.

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Dãy/khu, 1 ký tự (char(1)).</summary>
    public string Area { get; set; } = "A";
    public int Floor { get; set; }
    public int RoomNumber { get; set; }
    public decimal DefaultPrice { get; set; }
    public bool HasAirCondition { get; set; }
    public bool HasFridge { get; set; }
    public bool IsOccupied { get; set; }
    public bool HasWaterHeater { get; set; }
    public bool HasBed { get; set; }
    public bool HasWardrobe { get; set; }

    public List<ContractRoom> ContractRooms { get; set; } = [];
    public List<ContractTenant> ContractTenants { get; set; } = [];
    public List<UtilityBill> UtilityBills { get; set; } = [];
}

public class Company
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Representative { get; set; }
    public string? RepresentativePhone { get; set; }
    public string? RepresentativeEmail { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool? IsActive { get; set; }

    public List<Contract> Contracts { get; set; } = [];
    public List<Tenant> Tenants { get; set; } = [];
}

public class Contract
{
    public int Id { get; set; }
    public decimal PriceAgreed { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Active, Expired, Ended.</summary>
    public string Status { get; set; } = ContractStatus.Active;
    public string? Note { get; set; }
    public DateTime MoveInDate { get; set; }
    public decimal ElectricityPrice { get; set; }
    public decimal WaterPrice { get; set; }
    public string? ContractScanFilePath { get; set; }
    /// <summary>Individual hoặc Company.</summary>
    public string? ContractType { get; set; }
    public int? CompanyId { get; set; }

    public Company? Company { get; set; }
    public List<ContractRoom> ContractRooms { get; set; } = [];
    public List<ContractTenant> ContractTenants { get; set; } = [];
    public List<ContractExtensionHistory> ContractExtensionHistories { get; set; } = [];
    public List<IncomeExpense> IncomeExpenses { get; set; } = [];
    public List<UtilityBill> UtilityBills { get; set; } = [];

    public bool IsCompany => ContractType == Entities.ContractType.Company;
}

public class ContractRoom
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int RoomId { get; set; }
    public decimal PriceAgreed { get; set; }
    public string? Notes { get; set; }

    public Contract Contract { get; set; } = null!;
    public Room Room { get; set; } = null!;
}

public class Tenant
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string IdentityCard { get; set; } = "";
    public string? PhoneNumber { get; set; }
    /// <summary>Đường dẫn ảnh CCCD, nhiều ảnh cách nhau bằng dấu ;</summary>
    public string? Photo { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? PermanentAddress { get; set; }
    public string? Ethnicity { get; set; }
    public string? VehiclePlate { get; set; }
    public int? CompanyId { get; set; }

    public Company? Company { get; set; }
    public List<ContractTenant> ContractTenants { get; set; } = [];

    public IEnumerable<string> PhotoList =>
        string.IsNullOrWhiteSpace(Photo) ? [] : Photo.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class ContractTenant
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int TenantId { get; set; }
    public int ContractId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Room Room { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
}

public class ContractExtensionHistory
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public DateTime ExtendedAt { get; set; }
    public DateTime? OldEndDate { get; set; }
    public DateTime? NewEndDate { get; set; }
    public int? ExtendMonths { get; set; }
    public string? Note { get; set; }

    public Contract Contract { get; set; } = null!;
}

public class UtilityBill
{
    public int Id { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public int ContractId { get; set; }
    public int WaterIndexStart { get; set; }
    public int WaterIndexEnd { get; set; }
    public decimal ElectricityAmount { get; set; }
    public decimal WaterAmount { get; set; }
    public decimal RentAmount { get; set; }
    public decimal ExtraCharge { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? BillNote { get; set; }
    /// <summary>Draft hoặc Final.</summary>
    public string? BillStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int RoomId { get; set; }

    public Contract Contract { get; set; } = null!;
    public Room Room { get; set; } = null!;
}

public class IncomeExpenseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Income hoặc Expense.</summary>
    public string Type { get; set; } = CategoryType.Income;
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<IncomeExpense> IncomeExpenses { get; set; } = [];
}

public class IncomeExpense
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int? ContractId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public IncomeExpenseCategory Category { get; set; } = null!;
    public Contract? Contract { get; set; }
}

/// <summary>Cấu hình API key FPT.AI để quét CCCD (bảng FPTReaderAPI).</summary>
public class FptReaderApi
{
    public string ApiKey { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Status { get; set; }
}

public static class ContractStatus
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Ended = "Ended";

    public static string Display(string? status) => status switch
    {
        Active => "Đang hiệu lực",
        Expired => "Đã hết hạn",
        Ended => "Đã kết thúc",
        _ => status ?? ""
    };
}

public static class ContractType
{
    public const string Individual = "Individual";
    public const string Company = "Company";

    public static string Display(string? type) => type == Company ? "Công ty" : "Cá nhân";
}

public static class CategoryType
{
    public const string Income = "Income";
    public const string Expense = "Expense";

    public static string Display(string? type) => type == Expense ? "Chi" : "Thu";
}

/// <summary>Tên các danh mục thu chi hệ thống mà nghiệp vụ dựa vào.</summary>
public static class SystemCategories
{
    public const string RoomPayment = "Thu tiền phòng";
    public const string DepositRefund = "Trả tiền cọc";
}
