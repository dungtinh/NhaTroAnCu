using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class RoomHistoryViewModel
    {
        public Room Room { get; set; }
        public List<ContractHistoryItem> Contracts { get; set; }
        public List<TenantHistoryItem> Tenants { get; set; }
        public List<PaymentHistoryItem> Payments { get; set; }
        public ContractHistoryItem CurrentContract { get; set; }

        // Statistics
        public int TotalContracts { get; set; }
        public int TotalTenants { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalMonthsRented { get; set; }
        public int OccupancyRate { get; set; }
        public decimal AverageMonthlyRevenue { get; set; }
        public int AverageContractDuration { get; set; }
        public decimal TotalDebt { get; set; }
        public int PaymentCompleteRate { get; set; }
        public int AveragePaymentDelay { get; set; }
    }

    public class ContractHistoryItem
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; }
        public decimal MonthlyRent { get; set; }
        public string ContractType { get; set; }
        public List<TenantInfo> Tenants { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalDebt { get; set; }
        public int PaymentRate { get; set; }
        public string TenantNames { get; set; }
    }

    public class TenantInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
    }

    public class TenantHistoryItem
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string IdentityCard { get; set; }
        public string PhoneNumber { get; set; }
        public string Gender { get; set; }
        public DateTime MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class PaymentHistoryItem
    {
        public int ContractId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal BillAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Remaining { get; set; }
        public string Status { get; set; }
        public DateTime? PaymentDate { get; set; }
    }
}