using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaTroAnCu.Models
{
    public class ContractEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Loại hợp đồng")]
        public string ContractType { get; set; } // Individual or Company

        // Contract basic information
        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        [Display(Name = "Ngày bắt đầu")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        [Display(Name = "Ngày kết thúc")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày dọn vào")]
        [Display(Name = "Ngày dọn vào")]
        [DataType(DataType.Date)]
        public DateTime MoveInDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá điện")]
        [Display(Name = "Giá điện (VNĐ/kWh)")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá điện phải lớn hơn 0")]
        public decimal ElectricityPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá nước")]
        [Display(Name = "Giá nước (VNĐ/m³)")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá nước phải lớn hơn 0")]
        public decimal WaterPrice { get; set; }

        [Display(Name = "Ghi chú")]
        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự")]
        public string Note { get; set; }

        [Display(Name = "Trạng thái")]
        [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
        public string Status { get; set; }

        [Display(Name = "File scan hợp đồng")]
        public string ContractScanFilePath { get; set; }

        // For Company Contract
        public int? CompanyId { get; set; }
        public string CompanyName { get; set; }
        public List<RoomSelectionModel> SelectedRooms { get; set; }

        // For Individual Contract
        [Display(Name = "Phòng")]
        public int? RoomId { get; set; }

        [Display(Name = "Giá thuê thỏa thuận")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá thuê phải lớn hơn 0")]
        public decimal? PriceAgreed { get; set; }

        public List<TenantViewModel> Tenants { get; set; }

        public ContractEditViewModel()
        {
            SelectedRooms = new List<RoomSelectionModel>();
            Tenants = new List<TenantViewModel>();
        }
    }

}