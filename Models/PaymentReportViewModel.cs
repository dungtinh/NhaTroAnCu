using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class PaymentReportViewModel
    {
        public List<PaymentHistory> Payments { get; set; }
        public PaymentStatistics OverallStatistics { get; set; }  // Thống kê toàn bộ
        public PaymentStatistics FilteredStatistics { get; set; } // Thống kê theo điều kiện lọc
        public List<RoomPaymentStatistic> OverallRoomStatistics { get; set; } // Top phòng toàn bộ
        public decimal FilteredTotalAmount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? RoomId { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    public class PaymentStatistics
    {
        public decimal TotalRevenue { get; set; }
        public int TotalPayments { get; set; }
        public decimal AveragePayment { get; set; }
        public int RoomCount { get; set; }
    }

    public class RoomPaymentStatistic
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public int PaymentCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}