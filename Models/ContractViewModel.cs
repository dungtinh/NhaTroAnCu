// ContractCreateViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaTroAnCu.Models
{
    public class ContractCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn phòng")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tháng")]
        [Range(1, 120, ErrorMessage = "Số tháng phải từ 1 đến 120")]
        public int Months { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ngày chuyển vào")]
        [DataType(DataType.Date)]
        public DateTime MoveInDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá thuê")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá thuê phải lớn hơn 0")]
        public decimal PriceAgreed { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá điện")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá điện phải lớn hơn 0")]
        public decimal ElectricityPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá nước")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá nước phải lớn hơn 0")]
        public decimal WaterPrice { get; set; }

        public string Note { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tiền cọc phải lớn hơn hoặc bằng 0")]
        public decimal DepositAmount { get; set; }

        // Danh sách người thuê
        public List<TenantInputModel> Tenants { get; set; } = new List<TenantInputModel>();
    }

    public class TenantInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số CCCD")]
        public string IdentityCard { get; set; }

        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Gender { get; set; }
        public string PermanentAddress { get; set; }
        public string Ethnicity { get; set; }
        public string VehiclePlate { get; set; }
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
}