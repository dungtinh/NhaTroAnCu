using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
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
}