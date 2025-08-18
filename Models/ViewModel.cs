// Models/Company.cs
namespace NhaTroAnCu.Models
{
    using NhaTroAnCu.Controllers;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    
    public class ContractCreateViewModel
    {
        // Loại hợp đồng
        [Required(ErrorMessage = "Vui lòng chọn loại khách hàng")]
        public string ContractType { get; set; } // "Individual" hoặc "Company"

        // ===== THÔNG TIN CHUNG =====
        [Required(ErrorMessage = "Vui lòng nhập ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tháng")]
        [Range(1, 120, ErrorMessage = "Số tháng phải từ 1 đến 120")]
        public int Months { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày chuyển vào")]
        [DataType(DataType.Date)]
        public DateTime MoveInDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá điện")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá điện phải lớn hơn 0")]
        public decimal ElectricityPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá nước")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá nước phải lớn hơn 0")]
        public decimal WaterPrice { get; set; }

        public string Note { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phải lớn hơn hoặc bằng 0")]
        public decimal DepositAmount { get; set; }

        // ===== CHO KHÁCH HÀNG CÁ NHÂN =====
        public int? SingleRoomId { get; set; }
        public decimal? SingleRoomPrice { get; set; }
        public TenantInputModel IndividualTenant { get; set; }

        // ===== CHO KHÁCH HÀNG CÔNG TY =====
        public CompanyInputModel Company { get; set; }
        public List<RoomSelectionModel> SelectedRooms { get; set; } = new List<RoomSelectionModel>();
    }

    public class ContractEditViewModel
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int Months { get; set; }
        public DateTime MoveInDate { get; set; }
        public decimal PriceAgreed { get; set; }
        public decimal ElectricityPrice { get; set; }
        public decimal WaterPrice { get; set; }
        public string Note { get; set; }
        public List<TenantEditModel> Tenants { get; set; } = new List<TenantEditModel>();
    }

    public class TenantEditModel : TenantInputModel
    {
        public int Id { get; set; }
        public string Photo { get; set; }
    }

    public class CompanyInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên công ty")]
        public string CompanyName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã số thuế")]
        public string TaxCode { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên người đại diện")]
        public string Representative { get; set; }

        public string RepresentativePhone { get; set; }
        public string RepresentativeEmail { get; set; }
        public string BankAccount { get; set; }
        public string BankName { get; set; }
    }

    public class RoomSelectionModel
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public decimal DefaultPrice { get; set; }
        public decimal AgreedPrice { get; set; }
        public bool IsSelected { get; set; }
        public string Notes { get; set; }
    }

    // ViewModel cho Room Details khi thêm nhân viên
    

    public class ContractHistoryViewModel
    {
        public List<ContractHistoryItemViewModel> Items { get; set; }
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public string SearchName { get; set; }
        public string SearchCard { get; set; }
        public string SearchAddress { get; set; }
        public string SortField { get; set; }
        public string SortDirection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int RoomId { get; set; }
    }

    public class ContractHistoryItemViewModel
    {
        public string RoomName { get; set; }
        public string TenantNames { get; set; }
        public string IdentityCards { get; set; }
        public string Genders { get; set; }
        public string Addresses { get; set; }
        public string PhoneNumbers { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Note { get; set; }
    }
    public class IncomeExpenseListViewModel
    {
        public List<IncomeExpenseItemViewModel> Items { get; set; }
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string CategoryFilter { get; set; }
        public string TypeFilter { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
    }

    public class IncomeExpenseItemViewModel
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public bool CategoryIsSystem { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; }
        public string ReferenceNumber { get; set; }
        public string RoomName { get; set; }
        public string ContractInfo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateIncomeExpenseViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }

        public int? ContractId { get; set; }
        public int? RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tiền")]
        [Range(1, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày giao dịch")]
        public DateTime TransactionDate { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(50)]
        public string ReferenceNumber { get; set; }
    }

    public class CategoryManagementViewModel
    {
        public List<IncomeExpenseCategory> Categories { get; set; }
        public int IncomeCount { get; set; }
        public int ExpenseCount { get; set; }
    }
    public class RoomTenantManagementViewModel
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int ContractId { get; set; }
        public string CompanyName { get; set; }
        public string ContractType { get; set; } // "Company" hoặc "Individual"
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }

        // Danh sách người thuê hiện tại trong phòng
        public List<TenantViewModel> CurrentTenants { get; set; } = new List<TenantViewModel>();

        // Form thêm người thuê mới
        public TenantInputModel NewTenant { get; set; } = new TenantInputModel();

        // Thống kê
        public int MaxTenantCount { get; set; } = 4; // Giới hạn số người/phòng
        public bool CanAddMore => CurrentTenants.Count < MaxTenantCount;
    }    

    // ViewModel hiển thị thông tin người thuê (nhân viên công ty)
    public class TenantViewModel
    {
        public int Id { get; set; } // ContractTenant.Id
        public int TenantId { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string PermanentAddress { get; set; }
        public string Ethnicity { get; set; }
        public string VehiclePlate { get; set; }
        public string Photo { get; set; }
        public DateTime JoinDate { get; set; } // ContractTenant.CreatedAt

        // Computed properties
        public int Age => BirthDate.HasValue ?
            DateTime.Now.Year - BirthDate.Value.Year : 0;

        public string DisplayName => string.IsNullOrEmpty(FullName) ?
            "Chưa cập nhật" : FullName;
    }

    // Input model cho form thêm người thuê (nhân viên công ty)
    public class TenantInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số CCCD/CMND")]
        [RegularExpression(@"^\d{9,12}$", ErrorMessage = "CCCD phải là số từ 9-12 chữ số")]
        public string IdentityCard { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        public string Gender { get; set; }

        [StringLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự")]
        public string PermanentAddress { get; set; }

        [StringLength(50, ErrorMessage = "Dân tộc không được quá 50 ký tự")]
        public string Ethnicity { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{1,2}[-][0-9]{4,5}$",
            ErrorMessage = "Biển số xe không đúng định dạng (VD: 92A-12345)")]
        public string VehiclePlate { get; set; }
    }

    // ViewModel cho danh sách người thuê của công ty (dùng cho báo cáo)
    public class CompanyTenantListViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public string TaxCode { get; set; }

        // Nhóm người thuê theo phòng
        public List<RoomTenantGroup> RoomGroups { get; set; } = new List<RoomTenantGroup>();

        // Tổng số người thuê
        public int TotalTenants { get; set; }
        public int TotalRooms { get; set; }
        public decimal TotalMonthlyRent { get; set; }
    }

    public class RoomTenantGroup
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public decimal RentPrice { get; set; }
        public List<TenantViewModel> Tenants { get; set; } = new List<TenantViewModel>();
        public int TenantCount => Tenants.Count;
    }
}