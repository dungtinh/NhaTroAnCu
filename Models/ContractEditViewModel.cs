using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaTroAnCu.Models
{
    public class ContractEditViewModel
    {
        public int Id { get; set; }
        [Display(Name = "Phòng")]
        public int RoomId { get; set; }

        [Display(Name = "Ngày vào")]
        public DateTime MoveInDate { get; set; }
        [Display(Name = "Số tháng thuê")]
        public int Months { get; set; }
        [Display(Name = "Giá thương lượng")]
        public decimal PriceAgreed { get; set; }
        [Display(Name = "Tiền đặt cọc")]
        public decimal DepositAmount { get; set; }
        [Display(Name = "Giá điện")]
        public decimal ElectricityPrice { get; set; }
        [Display(Name = "Giá nước")]
        public decimal WaterPrice { get; set; }

        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        public List<TenantEditModel> Tenants { get; set; } = new List<TenantEditModel>();

        // Deposit status info - for display only
        public bool IsDepositCollected { get; set; }
        public decimal? CollectedDepositAmount { get; set; }
        public DateTime? DepositCollectionDate { get; set; }
    }

    public class TenantEditModel
    {
        public int Id { get; set; } // 0 nếu là tenant mới thêm
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }
        [Display(Name = "Số CCCD")]
        public string IdentityCard { get; set; }
        [Display(Name = "Điện thoại")]
        public string PhoneNumber { get; set; }
        [Display(Name = "Ngày sinh")]
        public DateTime? BirthDate { get; set; }
        [Display(Name = "Giới tính")]
        public string Gender { get; set; }
        [Display(Name = "Nơi thường trú")]
        public string PermanentAddress { get; set; }
        public string Photo { get; set; } // Đường dẫn file ảnh hiện tại (nếu có)
        [Display(Name = "Dân tộc")]
        public string Ethnicity { get; set; }        // Thêm mới
        [Display(Name = "Biển số xe")]
        public string VehiclePlate { get; set; }     // Thêm mới
    }
}