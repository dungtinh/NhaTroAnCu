using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NhaTroAnCu.Models
{
    public class ReportSummaryViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalRooms { get; set; }
        public int RentedRooms { get; set; }
        public int UnrentedRooms { get; set; }
        public int PaidRooms { get; set; }
        public int UnpaidRooms { get; set; }
        public int OverdueRooms { get; set; } // Thêm trường này
        public decimal TotalAmount { get; set; }
        public double Density { get; set; }
    }
}