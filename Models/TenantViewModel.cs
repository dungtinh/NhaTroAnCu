using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using DataType = System.ComponentModel.DataAnnotations.DataType;

namespace NhaTroAnCu.Models
{
    public class TenantViewModel
    {
        public int? Id { get; set; } // ContractTenant.Id
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
    public class EditTenantViewModel
    {
        public int ContractTenantId { get; set; }
        public int TenantId { get; set; }
        public int ContractId { get; set; }
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số CCCD/CMND")]
        [RegularExpression(@"^\d{9,12}$", ErrorMessage = "CCCD phải là số từ 9-12 chữ số")]
        [Display(Name = "Số CCCD/CMND")]
        public string IdentityCard { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Giới tính")]
        public string Gender { get; set; }

        [StringLength(500, ErrorMessage = "Địa chỉ không được quá 500 ký tự")]
        [Display(Name = "Địa chỉ thường trú")]
        public string PermanentAddress { get; set; }

        [StringLength(50, ErrorMessage = "Dân tộc không được quá 50 ký tự")]
        [Display(Name = "Dân tộc")]
        public string Ethnicity { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{1,2}[-][0-9]{4,5}$",
            ErrorMessage = "Biển số xe không đúng định dạng (VD: 92A-12345)")]
        [Display(Name = "Biển số xe")]
        public string VehiclePlate { get; set; }

        [Display(Name = "Ảnh CCCD/Hộ chiếu")]
        public string ExistingPhoto { get; set; }

        // Thông tin để hiển thị
        public string RoomName { get; set; }
        public string ContractCode { get; set; }
    }
}