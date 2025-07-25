using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class TenantContractListItemViewModel
    {
        public int OrderNumber { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string Gender { get; set; }
        public string PermanentAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string RoomName { get; set; }
        public string Photo { get; set; }
        public DateTime ContractSignedDate { get; set; }
        public DateTime MoveInDate { get; set; }
    }

    public class TenantContractListViewModel
    {
        public List<TenantContractListItemViewModel> Items { get; set; }
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
    }
}