using PagedList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NhaTroAnCu.Models
{
    public class ContractListViewModel
    {
        // Danh sách hợp đồng phân trang
        public IPagedList<Contract> Contracts { get; set; }

        // Filter parameters
        public string SearchTerm { get; set; }
        public string ContractType { get; set; }
        public string Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? CompanyId { get; set; }
        public string SortOrder { get; set; }
        public int? PageSize { get; set; }

        // Dropdown lists
        public IEnumerable<SelectListItem> ContractTypes { get; set; }
        public IEnumerable<SelectListItem> Statuses { get; set; }
        public SelectList Companies { get; set; }
        public IEnumerable<SelectListItem> PageSizes { get; set; }

        // Sort parameters
        public string IdSortParm { get; set; }
        public string DateSortParm { get; set; }
        public string StatusSortParm { get; set; }
        public string EndDateSortParm { get; set; }

        // Statistics
        public int TotalContracts { get; set; }
        public int ActiveContracts { get; set; }
        public int ExpiredContracts { get; set; }
        public int EndedContracts { get; set; }
        public int NearExpiryContracts { get; set; }
        public decimal TotalActiveValue { get; set; }
        public int TotalRoomsRented { get; set; }
        public int TotalTenants { get; set; }

        public ContractListViewModel()
        {
            // Initialize default values
            ContractTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả --" },
                new SelectListItem { Value = "Individual", Text = "Cá nhân/Hộ gia đình" },
                new SelectListItem { Value = "Company", Text = "Công ty" }
            };

            Statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Tất cả --" },
                new SelectListItem { Value = "Active", Text = "Đang hiệu lực" },
                new SelectListItem { Value = "Expired", Text = "Đã hết hạn" },
                new SelectListItem { Value = "Ended", Text = "Đã kết thúc" },
                new SelectListItem { Value = "NearExpiry", Text = "Sắp hết hạn (30 ngày)" }
            };

            PageSizes = new List<SelectListItem>
            {
                new SelectListItem { Value = "10", Text = "10" },
                new SelectListItem { Value = "25", Text = "25" },
                new SelectListItem { Value = "50", Text = "50" },
                new SelectListItem { Value = "100", Text = "100" }
            };
        }
    }
}